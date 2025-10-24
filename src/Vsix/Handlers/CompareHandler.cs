using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools
{
    internal static class CompareHandler
    {
        public static async Task GenerateAsync(Project project)
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

                var dacOptions = new DataApiBuilderOptions
                {
                    Dacpac = project.FullPath,
                    ConnectionString = dbInfo.ConnectionString,
                    DatabaseType = DatabaseType.SQLServerDacpac,
                };

                var dacpacInfo = await GetDacpacInfoAsync(dacOptions);

                if (dacpacInfo == null)
                {
                    return;
                }

                await VS.StatusBar.ShowMessageAsync("Comparing database schemas...");

                var result = await RunCompareAsync(info.DatabaseIsSource, dacOptions.Dacpac, dbInfo.ConnectionString);

                if (!string.IsNullOrEmpty(result))
                {
                    await VS.StatusBar.ShowMessageAsync("Comparison completed successfully");

                    await VS.Documents.OpenInPreviewTabAsync(result);
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

        private static async Task<string> RunCompareAsync(bool databaseIsSource, string dacpacPath, string connectionString)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetCompareAsync(databaseIsSource, dacpacPath, connectionString);
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

        private static async Task<DatabaseConnectionModel> GetDacpacInfoAsync(DataApiBuilderOptions options)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dbInfo = new DatabaseConnectionModel();

            dbInfo.DatabaseType = DatabaseType.SQLServerDacpac;
            dbInfo.ConnectionString = $"Data Source=(local);Initial Catalog={Path.GetFileNameWithoutExtension(options.Dacpac)};Integrated Security=true;";
            options.ConnectionString = dbInfo.ConnectionString;
            options.DatabaseType = dbInfo.DatabaseType;

            options.Dacpac = await SqlProjHelper.BuildSqlProjectAsync(options.Dacpac);
            if (string.IsNullOrEmpty(options.Dacpac))
            {
                VSHelper.ShowMessage("Unable to build the database project");
                return null;
            }

            return dbInfo;
        }

        private static async Task<(DatabaseConnectionModel DatabaseModel, bool DatabaseIsSource)> ChooseDataBaseConnectionAsync(DataApiBuilderOptions options)
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
                return (null, false);
            }

            if (pickDataSourceResult.Payload.Connection != null)
            {
                options.ConnectionString = pickDataSourceResult.Payload.Connection.ConnectionString;
                options.DatabaseType = pickDataSourceResult.Payload.Connection.DatabaseType;
            }

            return (pickDataSourceResult.Payload.Connection,
                pickDataSourceResult.Payload.GetDatabaseOptions);
        }

        private static List<CodeGenerationItem> GetCodeGenerationModes()
        {
            var list = new List<CodeGenerationItem>
            {
                new CodeGenerationItem { Key = 0, Value = "Compare" },
            };
            return list;
        }
    }
 }
