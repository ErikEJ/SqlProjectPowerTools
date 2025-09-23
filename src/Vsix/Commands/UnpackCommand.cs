namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidUnpack)]
    internal sealed class UnpackCommand : BaseCommand<UnpackCommand>
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
            await VS.MessageBox.ShowWarningAsync("UnpackCommand", "Button clicked");
        }
    }
}
