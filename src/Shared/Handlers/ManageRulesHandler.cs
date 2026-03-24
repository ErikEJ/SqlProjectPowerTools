using System;
using System.IO;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal static class ManageRulesHandler
    {
        public static async Task RunAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var rulesExpression = await project.GetAttributeAsync("CodeAnalysisRules") ?? string.Empty;
                var runCodeAnalysisValue = await project.GetAttributeAsync("RunSqlCodeAnalysis") ?? string.Empty;
                var runCodeAnalysis = string.Equals(runCodeAnalysisValue, "True", StringComparison.OrdinalIgnoreCase);

                var sqlServerVersion = await GetSqlServerVersionAsync(project);

                await VS.StatusBar.ShowMessageAsync("Loading code analysis rules...");

                var rulesJsonPath = await GetRulesPathAsync(sqlServerVersion, rulesExpression);

                await VS.StatusBar.ClearAsync();

                if (string.IsNullOrEmpty(rulesJsonPath) || !File.Exists(rulesJsonPath))
                {
                    VSHelper.ShowError("Failed to load code analysis rules. Please check that the CLI tool is available and .NET 8.0 runtime is installed.");
                    return;
                }

                var rules = ResultDeserializer.BuildRulesResult(rulesJsonPath);
                try
                {
                    File.Delete(rulesJsonPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var viewModel = new ManageRulesViewModel();
                viewModel.LoadRules(rules, runCodeAnalysis, rulesExpression);

                var dialog = new ManageRulesDialog(viewModel, project.Name ?? string.Empty);
                var result = dialog.ShowAndAwaitUserResponse();

                if (!result.ClosedByOK)
                {
                    return;
                }

                var (newRunCodeAnalysis, newRulesExpression) = viewModel.GetResult();

                await project.TrySetAttributeAsync("RunSqlCodeAnalysis", newRunCodeAnalysis ? "True" : "False");
                await project.TrySetAttributeAsync("CodeAnalysisRules", newRulesExpression);

                await VS.StatusBar.ShowMessageAsync("Code analysis rules updated.");
            }
            catch (Exception exception)
            {
                await VS.StatusBar.ClearAsync();
                VSHelper.ShowError(exception.Message);
            }
        }

        private static async Task<string> GetRulesPathAsync(string sqlServerVersion, string rulesExpression)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetRulesPathAsync(sqlServerVersion, rulesExpression);
        }

        private static async Task<string> GetSqlServerVersionAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // MsBuild.Sdk.SqlProj uses SqlServerVersion directly (e.g. "Sql160")
            var sqlServerVersion = await project.GetAttributeAsync("SqlServerVersion") ?? string.Empty;
            if (!string.IsNullOrEmpty(sqlServerVersion))
            {
                return sqlServerVersion;
            }

            // Classic .sqlproj / Microsoft.Build.Sql uses DSP property
            var dsp = await project.GetAttributeAsync("DSP") ?? string.Empty;
            var version = ParseVersionFromDsp(dsp);
            return version ?? "Sql160";
        }

        private static string ParseVersionFromDsp(string dsp)
        {
            if (string.IsNullOrEmpty(dsp))
            {
                return null;
            }

            // Extract version from e.g. "Microsoft.Data.Tools.Schema.Sql.Sql160DatabaseSchemaProvider"
            var marker = "DatabaseSchemaProvider";
            var markerIndex = dsp.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex <= 0)
            {
                return null;
            }

            var beforeMarker = dsp.Substring(0, markerIndex);
            var lastDot = beforeMarker.LastIndexOf('.');
            if (lastDot < 0 || lastDot >= beforeMarker.Length - 1)
            {
                return null;
            }

            return beforeMarker.Substring(lastDot + 1);
        }
    }
}
