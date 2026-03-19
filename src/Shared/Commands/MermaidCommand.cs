namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidErDiagram)]
    internal sealed class MermaidCommand : BaseCommand<MermaidCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Command.Enabled = await Command.IsEnabledForAnySqlProjectAsync();
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var project = await VS.Solutions.GetActiveProjectAsync();
            if (project != null)
            {
                await ErDiagramHandler.BuildErDiagramAsync(project);
            }
        }
    }
}
