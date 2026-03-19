using System.IO;
using System.Threading.Tasks;

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

                var dacpacPath = await SqlProjHelper.BuildSqlProjectAsync(project.FullPath);

                var result = await UnpackFilesAsync(dacpacPath, unpackPath);

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

        private static async Task<string> UnpackFilesAsync(string dacpacPath, string unpackPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await VS.StatusBar.ShowProgressAsync("Unpacking files", 1, 2);

            var launcher = new ProcessLauncher();

            var result = await launcher.GetUnpackAsync(dacpacPath, unpackPath);

            await VS.StatusBar.ShowProgressAsync("Unpacking files", 2, 2);

            return result;
        }
    }
}