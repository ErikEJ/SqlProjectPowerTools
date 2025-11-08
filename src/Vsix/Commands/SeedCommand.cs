namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidSeed)]
    internal sealed class SeedCommand : BaseCommand<SeedCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var isSdkSqlProject = await Command.IsEnabledForMsBuildSdkSqlProjectAsync();

                Command.Enabled = isSdkSqlProject;
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
