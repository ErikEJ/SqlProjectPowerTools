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
                var rulesExpression = await project.GetAttributeAsync("SqlCodeAnalysisRules")
                    ?? await project.GetAttributeAsync("CodeAnalysisRules")
                    ?? string.Empty;
                var runCodeAnalysisValue = await project.GetAttributeAsync("RunSqlCodeAnalysis") ?? string.Empty;
                var runCodeAnalysis = string.Equals(runCodeAnalysisValue, "True", StringComparison.OrdinalIgnoreCase);

                var sqlServerVersion = await project.GetSqlServerVersionAsync();

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
                catch (Exception)
                {
                    // Ignore
                }

                var hasRulesPackages = await project.HasRulesPackagesAsync();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var viewModel = new ManageRulesViewModel();
                viewModel.LoadRules(rules, runCodeAnalysis, rulesExpression, hasRulesPackages);

                var dialog = new ManageRulesDialog(viewModel, project.Name ?? string.Empty);
                var result = dialog.ShowAndAwaitUserResponse();

                if (!result.ClosedByOK)
                {
                    return;
                }

                var (newRunCodeAnalysis, newRulesExpression) = viewModel.GetResult();

                await project.TrySetAttributeAsync("RunSqlCodeAnalysis", newRunCodeAnalysis ? "True" : "False");

                if (project.IsMsBuildSdkSqlDatabaseProject())
                {
                    // MsBuild.Sdk.SqlProj uses CodeAnalysisRules
                    await project.SetPropertyDirectAsync("CodeAnalysisRules", newRulesExpression);
                }
                else
                {
                    // Classic .sqlproj / Microsoft.Build.Sql uses SqlCodeAnalysisRules
                    await project.SetPropertyDirectAsync("SqlCodeAnalysisRules", newRulesExpression);
                }

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
    }
}
