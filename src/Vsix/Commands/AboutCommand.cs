namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidAbout)]
    internal sealed class AboutCommand : BaseCommand<AboutCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            // TODO About should always be enabled, but this is an example of how to enable/disable a command
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Command.Enabled = await Command.IsEnabledForSqlProjectAsync();
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await VS.MessageBox.ShowWarningAsync("SQL Database Projects Power Tools", "Button clicked");
        }
    }
}
