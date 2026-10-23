using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DacFXToolLib.Pipelines;

namespace SqlProjectsPowerTools;

internal static class PipelineHandler
{
    public static async Task AddPipelineAsync(Project project)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        try
        {
            if (await VSHelper.IsDebugModeAsync())
            {
                VSHelper.ShowError("Cannot add a pipeline while debugging");
                return;
            }

            var projectDirectory = Path.GetDirectoryName(project.FullPath);
            var optionsPath = Path.Combine(projectDirectory, PipelineOptions.OptionsFileName);

            var options = PipelineOptionsExtensions.TryRead(optionsPath);
            var isNew = options == null;
            options ??= new PipelineOptions();

            await ApplyProjectFactsAsync(project, options, isNew);

            var viewModel = new PipelineWizardViewModel();
            viewModel.Load(options);

            var dialog = new PipelineWizardDialog(viewModel, project.Name ?? string.Empty);
            if (!dialog.ShowAndAwaitUserResponse())
            {
                return;
            }

            viewModel.ApplyTo(options);

            var (dacpacPath, dacpacBuildFailed) = await TryGetDacpacPathAsync(project);

            var outputRoot = FindRepositoryRoot(projectDirectory) ?? projectDirectory;

            var overwrite = false;
            var existing = PipelineOptions.GeneratedFileNames
                .Where(f => File.Exists(Path.Combine(outputRoot, f.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();

            if (existing.Count > 0)
            {
                overwrite = await VS.MessageBox.ShowConfirmAsync(
                    "Overwrite existing pipeline files?",
                    string.Join(Environment.NewLine, existing) + Environment.NewLine + Environment.NewLine + "Choose 'No' to only create missing files. Publish profiles are never overwritten.");
            }

            if (!await SaveOptionsAsync(project, optionsPath, options))
            {
                return;
            }

            await VS.StatusBar.ShowMessageAsync("Creating Azure DevOps pipeline...");
            await VS.StatusBar.StartAnimationAsync(StatusAnimation.Build);

            string result;
            try
            {
                var launcher = new ProcessLauncher();
                result = await launcher.GeneratePipelineAsync(optionsPath, project.FullPath, dacpacPath, outputRoot, overwrite);
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.Build);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var written = (result ?? string.Empty)
                .Split(['|'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

            var profiles = written.Where(p => p.EndsWith(".publish.xml", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (profiles.Length > 0)
            {
                await project.AddExistingFilesAsync(profiles);
            }

            foreach (var file in new[] { PipelineOptions.SetupGuideFileName, PipelineOptions.PipelineFileName })
            {
                var path = Path.Combine(outputRoot, file.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    await VS.Documents.OpenAsync(path);
                }
            }

            var statusMessage = written.Count == 0
                ? "No pipeline files created - all files already exist."
                : $"Azure DevOps pipeline created ({written.Count} files). Follow the checklist in {PipelineOptions.SetupGuideFileName}.";
            if (dacpacBuildFailed)
            {
                statusMessage += " Warning: Project build failed, SQLCMD variables are not included in the pipeline.";
            }

            await VS.StatusBar.ShowMessageAsync(statusMessage);
        }
        catch (AggregateException ae)
        {
            foreach (var innerException in ae.Flatten().InnerExceptions)
            {
                VSHelper.ShowError(innerException.Message);
            }
        }
        catch (Exception exception)
        {
            await VS.StatusBar.ClearAsync();
            VSHelper.ShowError(exception.Message);
        }
    }

    private static async Task ApplyProjectFactsAsync(Project project, PipelineOptions options, bool isNew)
    {
        if (project.IsMsBuildSdkSqlDatabaseProject())
        {
            options.ProjectStyle = PipelineProjectStyle.MsBuildSdkSqlProj;
        }
        else if (project.IsMicrosoftSdkSqlDatabaseProject())
        {
            options.ProjectStyle = PipelineProjectStyle.MicrosoftBuildSql;
        }
        else
        {
            options.ProjectStyle = PipelineProjectStyle.Classic;
        }

        var (runCodeAnalysis, _, sqlServerVersion) = await project.IsInSqlProjAsync();

        options.SqlServerVersion = sqlServerVersion;

        var dacpacPath = await project.GetDacpacPathAsync();
        if (!string.IsNullOrEmpty(dacpacPath))
        {
            options.DacpacName = Path.GetFileNameWithoutExtension(dacpacPath);
        }

        if (isNew)
        {
            options.IncludeCodeAnalysis = runCodeAnalysis;
            options.TargetPlatform = sqlServerVersion?.IndexOf("Azure", StringComparison.OrdinalIgnoreCase) >= 0
                ? PipelineTargetPlatform.AzureSqlDatabase
                : PipelineTargetPlatform.SqlServer;

            if (options.TargetPlatform == PipelineTargetPlatform.SqlServer)
            {
                options.FirewallStrategy = PipelineFirewallStrategy.None;
                options.AgentPool = PipelineAgentPool.SelfHosted;
                options.Authentication = PipelineAuthentication.SqlAuthentication;
            }

            options.Environments = new List<PipelineEnvironment>
            {
                PipelineEnvironment.Create("dev"),
                PipelineEnvironment.Create("test"),
                PipelineEnvironment.Create("prod"),
            };
        }
    }

    private static async Task<(string DacpacPath, bool BuildFailed)> TryGetDacpacPathAsync(Project project)
    {
        try
        {
            // Build to read the SQLCMD variables from the .dacpac
            var dacpacPath = await SqlProjHelper.BuildSqlProjectAsync(project.FullPath);
            var dacpacExists = !string.IsNullOrEmpty(dacpacPath) && File.Exists(dacpacPath);
            return (dacpacExists ? dacpacPath : null, !dacpacExists);
        }
        catch (InvalidOperationException)
        {
            return (null, true);
        }
    }

    private static string FindRepositoryRoot(string directory)
    {
        var current = new DirectoryInfo(directory);

        while (current != null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static async Task<bool> SaveOptionsAsync(Project project, string optionsPath, PipelineOptions options)
    {
        if (File.Exists(optionsPath) && File.GetAttributes(optionsPath).HasFlag(FileAttributes.ReadOnly))
        {
            VSHelper.ShowError($"Unable to save options, the file is readonly: {optionsPath}");
            return false;
        }

        var isNew = !File.Exists(optionsPath);

        File.WriteAllText(optionsPath, options.Write(), Encoding.UTF8);

        if (isNew)
        {
            await project.AddExistingFilesAsync(optionsPath);
        }

        return true;
    }
}
