using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools
{
    internal class SeedHandler
    {
        public async Task GenerateSeedScriptsAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot generate code while debugging");
                    return;
                }

                var options = new DataApiBuilderOptions();

                var info = await ChooseDataBaseConnectionAsync(options);

                if (info.DatabaseModel == null)
                {
                    return;
                }

                var dbInfo = await GetDatabaseInfoAsync(options);

                if (dbInfo == null)
                {
                    return;
                }

                options.ProjectPath = project.FullPath;

                if (!await LoadDataBaseObjectsAsync(options))
                {
                    await VS.StatusBar.ClearAsync();
                    return;
                }

                await GenerateScriptAsync(options, dbInfo.ConnectionString, project);
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
                VSHelper.ShowError(exception.Message);
            }
            finally
            {
                await VS.StatusBar.ClearAsync();
            }
        }

        private static async Task GenerateScriptAsync(DataApiBuilderOptions options, string connectionString, Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await VS.StatusBar.ShowProgressAsync("Getting seed data", 1, 2);

            var revEngRunner = new ProcessLauncher();

            var parts = options.Tables.FirstOrDefault()?.Name.Split('.');

            if (parts == null || parts.Length != 2)
            {
                VSHelper.ShowError("No table selected.");
                return;
            }

            var tableName = parts[1];
            var schema = parts[0];

            var result = await revEngRunner.CreateMergeScriptAsync(options.ProjectPath, connectionString, tableName, schema);

            await VS.StatusBar.ShowProgressAsync("Getting seed data", 2, 2);

            if (File.Exists(result))
            {
                await VS.Documents.OpenAsync(result);

                var projectDirectory = Path.GetDirectoryName(project.FullPath);

                var insertStatement = $":r ./{Path.GetFileName(result)}";

                var postDeployFilePath = Path.Combine(projectDirectory, "Post-Deployment", "Script.PostDeployment.sql");

                if (File.Exists(postDeployFilePath))
                {
                    var textLines = File.ReadAllLines(postDeployFilePath).ToList();

                    if (!textLines.Any(line => line.Trim().Equals(insertStatement, StringComparison.OrdinalIgnoreCase)))
                    {
                        textLines.Add(string.Empty);
                        textLines.Add(insertStatement);
                        File.WriteAllLines(postDeployFilePath, textLines, Encoding.UTF8);
                    }
                }
                else
                {
                    File.WriteAllLines(postDeployFilePath, new List<string> { insertStatement }, Encoding.UTF8);
                }

                project.AddDeployToProject("Post-Deployment/Script.PostDeployment.sql", "PostDeploy");
            }
        }

        private static async Task<List<TableModel>> GetTablesAsync(string connectionString)
        {
            var builder = new TableListBuilder(connectionString, DatabaseType.SQLServer, null);

            return await builder.GetTableDefinitionsAsync();
        }

        private static async Task<bool> LoadDataBaseObjectsAsync(DataApiBuilderOptions options)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IEnumerable<TableModel> predefinedTables = null;

            try
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.Build);

                predefinedTables = await GetTablesAsync(options.ConnectionString);

                predefinedTables = predefinedTables
                    .Where(t => t.ObjectType == ObjectType.Table);
            }
            catch (InvalidOperationException ex)
            {
                VSHelper.ShowError($"{ex.Message}");
                return false;
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.Build);
            }

            var preselectedTables = new List<SerializationTableModel>();

            await VS.StatusBar.ClearAsync();

            var ptd = PackageManager.Package.GetView<IPickTablesDialog>()
                              .AddTables(predefinedTables, new List<Schema>())
                              .PreselectTables(preselectedTables);

            var pickTablesResult = ptd.ShowAndAwaitUserResponse(true);

            options.Tables = pickTablesResult.Payload.Objects.ToList();
            return pickTablesResult.ClosedByOK;
        }

        private async Task<(DatabaseConnectionModel DatabaseModel, int FileGenerationMode, bool ImportSettings)> ChooseDataBaseConnectionAsync(DataApiBuilderOptions options)
        {
            var vsDataHelper = new VsDataHelper();
            var databaseList = await vsDataHelper.GetDataConnectionsAsync();

            var psd = PackageManager.Package.GetView<IPickServerDatabaseDialog>();

            if (databaseList != null && databaseList.Any())
            {
                psd.PublishConnections(databaseList.Select(m => new DatabaseConnectionModel
                {
                    ConnectionName = m.Value.ConnectionName,
                    ConnectionString = m.Value.ConnectionString,
                    DatabaseType = m.Value.DatabaseType,
                    DataConnection = m.Value.DataConnection,
                }));
            }

            psd.PublishFileGenerationMode(GetCodeGenerationModes());

            var pickDataSourceResult = psd.ShowAndAwaitUserResponse(true);
            if (!pickDataSourceResult.ClosedByOK)
            {
                return (null, 0, false);
            }

            if (pickDataSourceResult.Payload.Connection != null)
            {
                options.ConnectionString = pickDataSourceResult.Payload.Connection.ConnectionString;
                options.DatabaseType = pickDataSourceResult.Payload.Connection.DatabaseType;
            }

            return (pickDataSourceResult.Payload.Connection,
                pickDataSourceResult.Payload.CodeGenerationMode,
                pickDataSourceResult.Payload.GetDatabaseOptions);
        }

        private static async Task<DatabaseConnectionModel> GetDatabaseInfoAsync(DataApiBuilderOptions options)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dbInfo = new DatabaseConnectionModel();

            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                dbInfo.ConnectionString = options.ConnectionString;
                dbInfo.DatabaseType = options.DatabaseType;
            }

            if (dbInfo.DatabaseType == DatabaseType.Undefined)
            {
                VSHelper.ShowError($"Unsupported provider");
                return null;
            }

            return dbInfo;
        }

        private static List<CodeGenerationItem> GetCodeGenerationModes()
        {
            var list = new List<CodeGenerationItem>
            {
                new CodeGenerationItem { Key = 0, Value = "Seed" },
            };
            return list;
        }
    }
}
