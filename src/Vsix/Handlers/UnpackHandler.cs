using System;
using System.IO;
using System.Threading.Tasks;

namespace SqlProjectsPowerTools
{
    internal class UnpackHandler
    {
        public async Task UnpackAsync(string projectPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (await VSHelper.IsDebugModeAsync())
                {
                    VSHelper.ShowError("Cannot unpack while debugging");
                    return;
                }

                var projectDir = Path.GetDirectoryName(projectPath);

                // Let user select DACPAC file
                var dacpacPath = await SelectDacpacFileAsync();
                if (string.IsNullOrEmpty(dacpacPath))
                {
                    return;
                }

                // Use project directory as default output location
                var outputPath = Path.Combine(projectDir, "UnpackedSchema");

                await VS.StatusBar.ShowMessageAsync("Unpacking DACPAC...");

                var result = await RunUnpackAsync(dacpacPath, outputPath);

                if (result == "OK")
                {
                    VSHelper.ShowMessage($"DACPAC unpacked successfully to '{outputPath}'");
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

        private static async Task<string> RunUnpackAsync(string dacpacPath, string outputPath)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetUnpackAsync(dacpacPath, outputPath);
        }

        private static async Task<string> SelectDacpacFileAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select DACPAC file to unpack",
                Filter = "DACPAC files (*.dacpac)|*.dacpac|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            var result = dialog.ShowDialog();
            return result == true ? dialog.FileName : null;
        }
    }
}