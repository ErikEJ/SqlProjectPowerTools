using System.ComponentModel;
using System.Diagnostics;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidGettingStarted)]
    internal sealed class GettingStartedCommand : BaseCommand<GettingStartedCommand>
    {
#pragma warning disable S1075 // URIs should not be hardcoded
        private const string Url = "https://github.com/ErikEJ/SqlProjectPowerTools/blob/main/docs/getting-started.md";
#pragma warning restore S1075 // URIs should not be hardcoded
        private const string ErrorMessage = "Unable to launch browser for the Getting Started link.";

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
