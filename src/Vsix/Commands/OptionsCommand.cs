namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidOptions)]
    internal sealed class OptionsCommand : BaseCommand<OptionsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await VS.MessageBox.ShowWarningAsync("OptionsCommand", "Coming soon");
        }
    }
}
