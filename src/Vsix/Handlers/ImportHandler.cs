using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools
{
    internal class ImportHandler
    {
        public async Task GenerateAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot generate code while debugging");
                    return;
                }

                var outputPath = Path.GetDirectoryName(project.FullPath);

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

                await VS.StatusBar.ShowMessageAsync("Importing database schema...");

                var result = await RunImportAsync(info.FileGenerationMode, outputPath, dbInfo.ConnectionString);

                if (result == "OK")
                {
                    await VS.StatusBar.ShowMessageAsync("Import completed successfully");
                }

                if (info.ImportSettings)
                {
                    await VS.StatusBar.ShowMessageAsync("Importing database settings...");

                    var settingsPath = await RunGetDatabaseSettingsAsync(dbInfo.ConnectionString);

                    if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath))
                    {
                        await VS.StatusBar.ShowMessageAsync("Settings import failed!");
                        return;
                    }

                    await VS.StatusBar.ShowMessageAsync("Import completed successfully");
                    var settingsString = File.ReadAllText(settingsPath, Encoding.UTF8);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsString);
                    foreach (var setting in settings)
                    {
                        await VS.StatusBar.ShowMessageAsync($"Saving database settings: {setting.Key}...");
                        if (string.IsNullOrEmpty(setting.Value))
                        {
                            continue;
                        }

                        await project.TrySetAttributeAsync(setting.Key, setting.Value);
                    }
                }
            }
            catch (AggregateException ae)
            {
                foreach (var innerException in ae.Flatten().InnerExceptions)
                {
                    await VS.MessageBox.ShowErrorAsync("SQL Database Project Power Tools", innerException.Message);
                }
            }
            catch (Exception exception)
            {
                await VS.MessageBox.ShowErrorAsync("SQL Database Project Power Tools", exception.Message);
            }
            finally
            {
                await VS.StatusBar.ClearAsync();
            }
        }

        private static async Task<string> RunImportAsync(int filegenerationMode, string optionsPath, string connectionString)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetImportAsync(filegenerationMode, optionsPath, connectionString);
        }

        private static async Task<string> RunGetDatabaseSettingsAsync(string connectionString)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetDatabaseSettingsAsync(connectionString);
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

        private List<CodeGenerationItem> GetCodeGenerationModes()
        {
            // https://learn.microsoft.com/dotnet/api/microsoft.sqlserver.dac.dacextracttarget
            var list = new List<CodeGenerationItem>
            {
                new CodeGenerationItem { Key = 5, Value = "SchemaObjectType" },
                new CodeGenerationItem { Key = 3, Value = "ObjectType" },
                new CodeGenerationItem { Key = 2, Value = "Flat" },
                new CodeGenerationItem { Key = 4, Value = "Schema" },
            };
            return list;
        }
    }
 }
