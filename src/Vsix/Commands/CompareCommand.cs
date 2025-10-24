namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidCompare)]
    internal sealed class CompareCommand : BaseCommand<CompareCommand>
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
                await CompareHandler.GenerateAsync(project);
            }
        }
    }
}
