using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlProjectsPowerTools;

internal static class AutoPublishOnSaveHandler
{
    private const string AutoPublishConnectionStringKey = "AutoPublish";
    private const string AutoPublishEnvSampleContents =
        AutoPublishConnectionStringKey + "=Server=localhost;Database=YourDatabase;Integrated Security=true;TrustServerCertificate=true;";

    private static int initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref initialized, 1) == 1)
        {
            return;
        }

        VS.Events.DocumentEvents.Saved += OnDocumentSaved;
    }

    public static async Task EnableAsync(Project project)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var options = await ToolOptions.GetLiveInstanceAsync();
        options.PublishProgrammabilityObjectsOnSave = true;
        await options.SaveAsync();

        var projectDirectory = Path.GetDirectoryName(project?.FullPath);
        var createdEnvFile = EnsureSampleEnvFile(projectDirectory);

        await VS.StatusBar.ShowMessageAsync(createdEnvFile
            ? "Publish on save enabled. Sample .env file created."
            : "Publish on save enabled.");
    }

    private static void OnDocumentSaved(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || !string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
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
            if (!options.PublishProgrammabilityObjectsOnSave)
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

            if (!TryGetConnectionStringFromEnvFile(projectDirectory, AutoPublishConnectionStringKey, out var connectionString))
            {
                return;
            }

            var script = await ReadScriptAsync(filePath);
            if (!TryCreateAutoPublishScript(script, out var scriptToPublish))
            {
                return;
            }

            await ExecuteScriptAsync(connectionString, scriptToPublish);
            await VS.StatusBar.ShowMessageAsync($"Publish completed: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            await ex.LogAsync();
            await VS.StatusBar.ShowMessageAsync($"Publish failed: {Path.GetFileName(filePath)}");
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

    private static bool EnsureSampleEnvFile(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return false;
        }

        var envPath = Path.Combine(projectDirectory, ".env");
        if (File.Exists(envPath))
        {
            return false;
        }

        File.WriteAllText(envPath, AutoPublishEnvSampleContents);
        return true;
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

        var statements = tsqlScript.Batches
            .SelectMany(b => b.Statements)
            .ToList();

        if (statements.Any(s => !IsAllowedStatement(s)))
        {
            return false;
        }

        var createStatements = statements
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

            // Avoid producing "CREATE OR ALTER OR ALTER" when the script already uses CREATE OR ALTER.
            var lookaheadIndex = insertIndex;
            while (lookaheadIndex < script.Length && char.IsWhiteSpace(script[lookaheadIndex]))
            {
                lookaheadIndex++;
            }

            if (lookaheadIndex + 2 <= script.Length
                && string.Equals(script.Substring(lookaheadIndex, 2), "OR", StringComparison.OrdinalIgnoreCase))
            {
                lookaheadIndex += 2;
                while (lookaheadIndex < script.Length && char.IsWhiteSpace(script[lookaheadIndex]))
                {
                    lookaheadIndex++;
                }

                if (lookaheadIndex + 5 <= script.Length
                    && string.Equals(script.Substring(lookaheadIndex, 5), "ALTER", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            rewrittenScript = rewrittenScript.Insert(insertIndex, " OR ALTER");
        }

        scriptToPublish = rewrittenScript;
        return true;
    }

    private static async Task<string> ReadScriptAsync(string filePath)
    {
        Exception lastException = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(50);
            }
        }

        throw lastException ?? new IOException($"Failed to read SQL file '{filePath}'.", lastException);
    }

    private static bool IsAllowedStatement(TSqlStatement statement)
    {
        return IsSupportedCreateStatement(statement)
            || statement is SetOnOffStatement;
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
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                    command.CommandText = batch;
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
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
}
