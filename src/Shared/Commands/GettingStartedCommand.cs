using System.ComponentModel;
using System.Diagnostics;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidGettingStarted)]
    internal sealed class GettingStartedCommand : BaseCommand<GettingStartedCommand>
    {
        private const string Url = "https://github.com/ErikEJ/SqlProjectPowerTools/blob/main/docs/getting-started.md";
        private const string ErrorMessage = "Unable to launch browser for the Getting Started link.";

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                if (Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }) == null)
                {
                    await VS.MessageBox.ShowErrorAsync("SQL Database Project Power Tools", ErrorMessage, buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or PlatformNotSupportedException)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync("SQL Database Project Power Tools", ErrorMessage, buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
        }
    }
}
