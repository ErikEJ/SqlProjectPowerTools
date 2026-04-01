#if SSMS
using System.ComponentModel;
using System.Diagnostics;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidMoreExtensions)]
    internal sealed class MoreExtensionsCommand : BaseCommand<MoreExtensionsCommand>
    {
#pragma warning disable S1075 // URIs should not be hardcoded
        private const string Url = "https://github.com/brink-daniel/ssms-object-explorer-menu/blob/main/SSMSExtensionList.md";
#pragma warning restore S1075 // URIs should not be hardcoded
        private const string ErrorMessage = "Unable to launch browser for the More extensions link.";

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                if (Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }) == null)
                {
                    await VS.MessageBox.ShowErrorAsync("SQL Database Project Power Tools", ErrorMessage);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or PlatformNotSupportedException)
            {
                await ex.LogAsync();
                await VS.MessageBox.ShowErrorAsync("SQL Database Project Power Tools", ErrorMessage);
            }
        }
    }
}
#endif
