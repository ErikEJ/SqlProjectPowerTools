using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools
{
    internal static class DabBuilderHandler
    {
        public static async System.Threading.Tasks.Task BuildDabConfigAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot generate code while debugging");
                    return;
                }

                var projectPath = Path.GetDirectoryName(project.FullPath);

                var optionsPath = Path.Combine(projectPath, "dab-options.json");

                var options = DataApiBuilderOptionsExtensions.TryRead(optionsPath);

                if (options == null)
                {
                    options = new DataApiBuilderOptions();
                }

                options.ProjectPath = Path.GetDirectoryName(project.FullPath);
                options.Dacpac = project.FullPath;

                DatabaseConnectionModel dbInfo = null;

                dbInfo = await GetDatabaseInfoAsync(options);

                if (dbInfo == null)
                {
                    await VS.StatusBar.ClearAsync();
                    return;
                }

                if (!await LoadDataBaseObjectsAsync(options))
                {
                    await VS.StatusBar.ClearAsync();
                    return;
                }

                await SaveOptionsAsync(project, optionsPath, options);

                await GenerateFilesAsync(optionsPath, dbInfo.ConnectionString);

                options.ConnectionString = null;

                if (!File.Exists(Path.Combine(options.ProjectPath, ".env"))
                    && await VS.MessageBox.ShowConfirmAsync("Create .env file with your connection string?", "Remember to exclude from source control!"))
                {
                    File.WriteAllText(Path.Combine(options.ProjectPath, ".env"), $"dab-connection-string={dbInfo.ConnectionString}");
                }
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

            await VS.StatusBar.ShowProgressAsync("Creating DAB configuration", 1, 3);

            await VS.StatusBar.ShowProgressAsync("Creating DAB configuration", 2, 3);

            var revEngRunner = new ProcessLauncher();
            var cmdPath = await revEngRunner.GetDabConfigPathAsync(optionsPath, connectionString);

            await VS.StatusBar.ShowProgressAsync("Creating DAB configuration", 3, 3);

            if (File.Exists(cmdPath))
            {
                await VS.Documents.OpenAsync(cmdPath);
            }
        }

        private static async System.Threading.Tasks.Task SaveOptionsAsync(Project project, string optionsPath, DataApiBuilderOptions options)
        {
            if (File.Exists(optionsPath) && File.GetAttributes(optionsPath).HasFlag(FileAttributes.ReadOnly))
            {
                VSHelper.ShowError($"Unable to save options, the file is readonly: {optionsPath}");
                return;
            }

            if (!File.Exists(optionsPath + ".ignore"))
            {
                File.WriteAllText(optionsPath, options.Write(), Encoding.UTF8);

                await project.AddExistingFilesAsync(new List<string> { optionsPath }.ToArray());
            }
        }

        private static async Task<List<TableModel>> GetDacpacTablesAsync(string dacpacPath)
        {
            var builder = new TableListBuilder(dacpacPath, DatabaseType.SQLServerDacpac, null);

            return await builder.GetTableDefinitionsAsync();
        }


        private static async Task<bool> LoadDataBaseObjectsAsync(DataApiBuilderOptions options)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IEnumerable<TableModel> predefinedTables = null;

            try
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.Build);

                predefinedTables = await GetDacpacTablesAsync(options.Dacpac);

                predefinedTables = predefinedTables
                    .Where(t => t.ObjectType == ObjectType.Table
                    || t.ObjectType == ObjectType.View
                    || (t.ObjectType == ObjectType.Procedure &&
                        (options.DatabaseType == DatabaseType.SQLServer || options.DatabaseType == DatabaseType.SQLServerDacpac)));
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