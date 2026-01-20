namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidDeployScript)]
    internal sealed class DeployScriptCommand : BaseCommand<DeployScriptCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Command.Enabled = await Command.IsEnabledForMsBuildSdkSqlProjectAsync();
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var project = await VS.Solutions.GetActiveProjectAsync();
            if (project != null)
            {
                await DeployScriptHandler.AddDeploymentScriptsAsync(project);
            }
        }
    }
}
