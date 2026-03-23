namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidSeed)]
    internal sealed class SeedCommand : BaseCommand<SeedCommand>
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
                var handler = new SeedHandler();
                await handler.GenerateSeedScriptsAsync(project);
            }
        }
    }
}
