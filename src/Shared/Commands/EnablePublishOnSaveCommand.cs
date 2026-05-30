namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidEnablePublishOnSave)]
    internal sealed class EnablePublishOnSaveCommand : BaseCommand<EnablePublishOnSaveCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var isEnabled = await Command.IsEnabledForAnySqlProjectAsync();

                if (isEnabled)
                {
                    var options = await ToolOptions.GetLiveInstanceAsync();
                    isEnabled = !options.PublishProgrammabilityObjectsOnSave;
                }

                Command.Enabled = isEnabled;
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var project = await VS.Solutions.GetActiveProjectAsync();
            if (project != null)
            {
                await AutoPublishOnSaveHandler.EnableAsync(project);
            }
        }
    }
}
