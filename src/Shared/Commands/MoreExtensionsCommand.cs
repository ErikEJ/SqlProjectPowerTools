using System.ComponentModel;
using System.Diagnostics;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidMoreExtensions)]
    internal sealed class MoreExtensionsCommand : BaseCommand<MoreExtensionsCommand>
    {
        private const string Url = "https://github.com/brink-daniel/ssms-object-explorer-menu/blob/main/SSMSExtensionList.md";
        private const string ErrorMessage = "Unable to launch browser for the More extensions link.";

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                if (Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }) == null)
                {
                    await VS.MessageBox.ShowAsync("SQL Database Project Power Tools", ErrorMessage, buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or PlatformNotSupportedException)
            {
                await VS.MessageBox.ShowAsync("SQL Database Project Power Tools", ErrorMessage, buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK);
            }
        }
    }
}
