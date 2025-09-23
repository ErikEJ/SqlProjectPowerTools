namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidAnalyze)]
    internal sealed class AnalyzeCommand : BaseCommand<AnalyzeCommand>
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
            var project = await VS.Solutions.GetActiveProjectAsync();
            if (project != null)
            {
                await DacpacAnalyzerHandler.GenerateAsync(project.FullPath);
                return;
            }
        }
    }
}
