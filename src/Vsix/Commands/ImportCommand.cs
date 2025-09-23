namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidImport)]
    internal sealed class ImportCommand : BaseCommand<ImportCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Command.Enabled = await Command.IsEnabledForSqlProjectAsync();
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await VS.MessageBox.ShowWarningAsync("ImportCommand", "Coming soon");
        }
    }
}
