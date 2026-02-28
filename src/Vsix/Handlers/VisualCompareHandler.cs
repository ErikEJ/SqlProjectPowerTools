using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlProjectsPowerTools
{
    internal static class VisualCompareHandler
    {
        public static async Task RunAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot run schema compare while debugging");
                    return;
                }

                var options = new DataApiBuilderOptions();

                var connectionInfo = await ChooseDataBaseConnectionAsync(options);

                if (connectionInfo.DatabaseModel == null)
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

                var dacpacInfo = await HandlerHelper.GetDatabaseInfoAsync(dacOptions);

                if (dacpacInfo == null)
                {
                    return;
                }

                await VS.StatusBar.ShowMessageAsync("Comparing database schemas...");

                string jsonResultPath = null;
                IVsThreadedWaitDialog2 dialog = null;

                try
                {
                    var dialogFactory = await VS.GetServiceAsync<SVsThreadedWaitDialogFactory, IVsThreadedWaitDialogFactory>();
                    dialogFactory?.CreateInstance(out dialog);

                    dialog?.StartWaitDialog(
                        szWaitCaption: "SQL Database Project Power Tools",
                        szWaitMessage: "Comparing database schemas...",
                        szProgressText: null,
                        varStatusBmpAnim: null,
                        szStatusBarText: "Comparing database schemas...",
                        iDelayToShowDialog: 0,
                        fIsCancelable: false,
                        fShowMarqueeProgress: true);

                    jsonResultPath = await RunVisualCompareAsync(connectionInfo.DatabaseIsSource, dacOptions.Dacpac, dbInfo.ConnectionString);
                }
                finally
                {
                    dialog?.EndWaitDialog(out int _);
                }

                if (!string.IsNullOrEmpty(jsonResultPath))
                {
                    var result = ResultDeserializer.BuildVisualCompareResult(jsonResultPath);
                    ShowResultDialog(result, project.Name);
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

        private static void ShowResultDialog(VisualCompareResult result, string projectName)
        {
            var dlg = new SchemaCompareDialog(result, projectName);
            dlg.ShowModal();
        }

        private static async Task<string> RunVisualCompareAsync(bool databaseIsSource, string dacpacPath, string connectionString)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetVisualCompareAsync(databaseIsSource, dacpacPath, connectionString);
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
                VSHelper.ShowError("Unsupported provider");
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
            return new List<CodeGenerationItem>
            {
                new CodeGenerationItem { Key = 0, Value = "Compare" },
            };
        }
    }
}
