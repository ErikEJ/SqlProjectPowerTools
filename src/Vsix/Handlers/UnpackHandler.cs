using System.IO;
using System.Threading.Tasks;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;

namespace SqlProjectsPowerTools
{
    internal static class UnpackHandler
    {
        public static async System.Threading.Tasks.Task UnpackDacpacAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot unpack DACPAC while debugging");
                    return;
                }

                var projectPath = Path.GetDirectoryName(project.FullPath);

                var unpackPath = Path.Combine(projectPath, DateTime.Now.ToString("yyyy-MM-dd_HH-mm"));

                var options = new DataApiBuilderOptions();

                options.Dacpac = project.FullPath;

                var dbInfo = await GetDatabaseInfoAsync(options);

                if (dbInfo == null)
                {
                    return;
                }

                var result = await UnpackFilesAsync(unpackPath, dbInfo.ConnectionString);

                if (result == "OK")
                {
                    VSHelper.ShowMessage($"Unpack completed to '{unpackPath}'");
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

        private static async Task<string> UnpackFilesAsync(string unpackPath, string connectionString)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await VS.StatusBar.ShowProgressAsync("Unpacking files", 1, 2);

            var launcher = new ProcessLauncher();

            var result = await launcher.GetUnpackAsync(unpackPath, connectionString);

            await VS.StatusBar.ShowProgressAsync("Unpacking files", 2, 2);

            return result;
        }
    }
}