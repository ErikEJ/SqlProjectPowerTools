namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidVisualCompare)]
    internal sealed class VisualCompareCommand : BaseCommand<VisualCompareCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Command.Enabled = await Command.IsEnabledForModernSqlProjectAsync();
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var project = await VS.Solutions.GetActiveProjectAsync();
            if (project != null)
            {
                await VisualCompareHandler.RunAsync(project);
            }
        }
    }
}
