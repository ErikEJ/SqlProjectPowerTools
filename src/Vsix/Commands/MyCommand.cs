namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidAbout)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await VS.MessageBox.ShowWarningAsync("SQL Database Projects Power Tools", "Button clicked");
        }
    }
}
