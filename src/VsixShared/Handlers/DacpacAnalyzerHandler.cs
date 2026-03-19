using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlProjectsPowerTools
{
    internal static class DacpacAnalyzerHandler
    {
        public static async Task GenerateAsync(string path)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                await VS.StatusBar.ShowProgressAsync("Generating DACPAC Analysis report...", 1, 3);

                var dacpacPath = await SqlProjHelper.BuildSqlProjectAsync(path);

                await VS.StatusBar.ShowProgressAsync("Generating DACPAC Analysis report...", 2, 3);

                var reportPath = await GetDacpacReportAsync(dacpacPath);

                await VS.StatusBar.ShowProgressAsync("Generating DACPAC Analysis report...", 3, 3);

                if (File.Exists(reportPath))
                {
                    await OpenVsWebBrowserAsync(reportPath);
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

        private static async Task<string> GetDacpacReportAsync(string path)
        {
            var launcher = new ProcessLauncher();
            return await launcher.GetReportPathAsync(path);
        }

        private static void OpenWebBrowser(string path)
        {
            Process.Start(path);
        }

        private static async Task OpenVsWebBrowserAsync(string path)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var service = await VS.GetServiceAsync<SVsWebBrowsingService, IVsWebBrowsingService>();

            if (service == null)
            {
                OpenWebBrowser(path);
                return;
            }

            service.Navigate(path, (uint)__VSWBNAVIGATEFLAGS.VSNWB_ForceNew, out var frame);
            frame.Show();
        }
    }
}