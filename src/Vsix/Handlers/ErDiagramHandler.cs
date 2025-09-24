using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools
{
    internal class ErDiagramHandler
    {
        public async System.Threading.Tasks.Task BuildErDiagramAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot generate code while debugging");
                    return;
                }

                var optionsPath = Path.Combine(Path.GetTempPath(), "dacfx-erdiagram-options.json");

                var options = new DataApiBuilderOptions();

                options.ProjectPath = Path.GetDirectoryName(project.FullPath);

                var version = await VS.Shell.GetVsVersionAsync();

                if (version >= new Version(18, 0))
                {
                    options.Optional = true;
                }

                options.Dacpac = project.FullPath;

                var dbInfo = await GetDatabaseInfoAsync(options);

                if (dbInfo == null)
                {
                    await VS.StatusBar.ClearAsync();
                    return;
                }

                await VS.StatusBar.ShowMessageAsync("Loading database objects...");

                if (!await LoadDataBaseObjectsAsync(options, dbInfo))
                {
                    await VS.StatusBar.ClearAsync();
                    return;
                }

                SaveOptions(optionsPath, options);

                await GenerateFilesAsync(optionsPath, dbInfo.ConnectionString);
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
        }

        private static async Task<DatabaseConnectionModel> GetDatabaseInfoAsync(DataApiBuilderOptions options)
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

        private static async System.Threading.Tasks.Task GenerateFilesAsync(string optionsPath, string connectionString)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await VS.StatusBar.ShowProgressAsync("Creating diagram", 1, 2);

            var launcher = new ProcessLauncher();
            var diagramPath = await launcher.GetErDiagramAsync(optionsPath, connectionString);

            await VS.StatusBar.ShowProgressAsync("Creating diagram", 2, 2);

            if (File.Exists(diagramPath))
            {
                await VS.Documents.OpenAsync(diagramPath);
            }

            await VS.StatusBar.ShowMessageAsync("Diagram created.");
        }

        private static void SaveOptions(string optionsPath, DataApiBuilderOptions options)
        {
            File.WriteAllText(optionsPath, options.Write(), Encoding.UTF8);
        }

        private static async Task<List<TableModel>> GetDacpacTablesAsync(string dacpacPath)
        {
            var builder = new TableListBuilder(dacpacPath, DatabaseType.SQLServerDacpac, null);

            return await builder.GetTableDefinitionsAsync();
        }

        private async Task<bool> LoadDataBaseObjectsAsync(DataApiBuilderOptions options, DatabaseConnectionModel dbInfo)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IEnumerable<TableModel> predefinedTables = null;

            try
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.Build);

                predefinedTables = await GetDacpacTablesAsync(options.Dacpac);

                predefinedTables = predefinedTables
                    .Where(t => t.ObjectType == ObjectType.Table
                    || t.ObjectType == ObjectType.View);
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
    }
}