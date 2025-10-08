using System.Diagnostics;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidAbout)]
    internal sealed class AboutCommand : BaseCommand<AboutCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(VsixPackage).Assembly.Location).FileVersion ?? "N/A";

            await VS.MessageBox.ShowAsync("SQL Database Project Power Tools",  $"Version {version} from ErikEJ - https://github.com/ErikEJ", buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK);
        }
    }
}
