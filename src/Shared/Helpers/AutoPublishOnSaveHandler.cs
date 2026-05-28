using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlProjectsPowerTools
{
    internal static class AutoPublishOnSaveHandler
    {
        private static readonly Guid OutputPaneGuid = new("DA2D7543-F81F-443B-8743-8C508C5601D9");
        private static int initialized;

        public static void Initialize()
        {
            if (Interlocked.Exchange(ref initialized, 1) == 1)
            {
                return;
            }

            VS.Events.DocumentEvents.Saved += OnDocumentSaved;
        }

        private static void OnDocumentSaved(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)
                || !filePath.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => TryAutoPublishAsync(filePath));
        }

        private static async Task TryAutoPublishAsync(string filePath)
        {
            try
            {
                var options = await ToolOptions.GetLiveInstanceAsync();
                if (!options.EnableAutoPublishOnSave)
                {
                    return;
                }

                var project = await GetSqlProjectForFileAsync(filePath);
                if (project == null)
                {
                    return;
                }

                var projectDirectory = Path.GetDirectoryName(project.FullPath);
                if (string.IsNullOrWhiteSpace(projectDirectory))
                {
                    return;
                }

                if (!TryGetConnectionStringFromEnvFile(projectDirectory, "AutoPublish", out var connectionString))
                {
                    return;
                }

                var script = await File.ReadAllTextAsync(filePath);
                if (!TryCreateAutoPublishScript(script, out var scriptToPublish))
                {
                    return;
                }

                await ExecuteScriptAsync(connectionString, scriptToPublish);
                await VS.StatusBar.ShowMessageAsync($"Auto publish completed: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
                await LogToExtensionsOutputAsync($"Auto publish failed for '{filePath}': {ex}");
                await VS.StatusBar.ShowMessageAsync($"Auto publish failed: {Path.GetFileName(filePath)}");
            }
        }

        private static async Task<Project> GetSqlProjectForFileAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var absolutePath = Path.GetFullPath(filePath);
            var projects = await VS.Solutions.GetAllProjectsAsync();

            return projects
                .Where(p => p.IsAnySqlDatabaseProject())
                .FirstOrDefault(p => IsPathUnderDirectory(absolutePath, Path.GetDirectoryName(p.FullPath)));
        }

        private static bool IsPathUnderDirectory(string filePath, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            var fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (!fullDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullDirectoryPath += Path.DirectorySeparatorChar;
            }

            return filePath.StartsWith(fullDirectoryPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetConnectionStringFromEnvFile(string projectDirectory, string key, out string connectionString)
        {
            connectionString = null;

            var envPath = Path.Combine(projectDirectory, ".env");
            if (!File.Exists(envPath))
            {
                return false;
            }

            foreach (var line in File.ReadLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var equalsIndex = trimmed.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                var currentKey = trimmed.Substring(0, equalsIndex).Trim();
                if (!currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                connectionString = trimmed.Substring(equalsIndex + 1).Trim().Trim('"', '\'');
                return !string.IsNullOrWhiteSpace(connectionString);
            }

            return false;
        }

        private static bool TryCreateAutoPublishScript(string script, out string scriptToPublish)
        {
            scriptToPublish = null;

            if (string.IsNullOrWhiteSpace(script))
            {
                return false;
            }

            var parser = new TSql170Parser(initialQuotedIdentifiers: true);
            TSqlFragment fragment;
            IList<ParseError> errors;

            using (var reader = new StringReader(script))
            {
                fragment = parser.Parse(reader, out errors);
            }

            if (errors?.Count > 0 || fragment is not TSqlScript tsqlScript)
            {
                return false;
            }

            var createStatements = tsqlScript.Batches
                .SelectMany(b => b.Statements)
                .Where(IsSupportedCreateStatement)
                .ToList();

            if (createStatements.Count == 0)
            {
                return false;
            }

            var rewrittenScript = script;
            foreach (var statement in createStatements.OrderByDescending(s => s.StartOffset))
            {
                var createToken = tsqlScript.ScriptTokenStream?[statement.FirstTokenIndex];
                if (createToken == null || createToken.TokenType != TSqlTokenType.Create)
                {
                    continue;
                }

                var insertIndex = createToken.Offset + createToken.Text.Length;
                rewrittenScript = rewrittenScript.Insert(insertIndex, " OR ALTER");
            }

            scriptToPublish = rewrittenScript;
            return true;
        }

        private static bool IsSupportedCreateStatement(TSqlStatement statement)
        {
            return statement is CreateProcedureStatement
                or CreateViewStatement
                or CreateFunctionStatement
                or CreateTriggerStatement;
        }

        private static async Task ExecuteScriptAsync(string connectionString, string script)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                foreach (var batch in SplitBatches(script))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = CommandType.Text;
                        command.CommandText = batch;
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private static IEnumerable<string> SplitBatches(string script)
        {
            var parser = new TSql170Parser(initialQuotedIdentifiers: true);
            TSqlFragment fragment;
            IList<ParseError> errors;

            using (var reader = new StringReader(script))
            {
                fragment = parser.Parse(reader, out errors);
            }

            if (errors?.Count > 0 || fragment is not TSqlScript tsqlScript)
            {
                return new[] { script };
            }

            return tsqlScript.Batches
                .Select(batch => script.Substring(batch.StartOffset, batch.FragmentLength).Trim())
                .Where(batch => !string.IsNullOrWhiteSpace(batch))
                .ToList();
        }

        private static async Task LogToExtensionsOutputAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return;
            }

            var paneGuid = OutputPaneGuid;
            outputWindow.CreatePane(ref paneGuid, "SQL Database Project Power Tools", 1, 1);
            outputWindow.GetPane(ref paneGuid, out var pane);
            pane?.OutputStringThreadSafe($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
            pane?.Activate();
        }
    }
}
