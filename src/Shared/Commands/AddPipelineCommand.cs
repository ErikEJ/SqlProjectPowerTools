namespace SqlProjectsPowerTools;

[Command(PackageIds.cmdidAddPipeline)]
internal sealed class AddPipelineCommand : BaseCommand<AddPipelineCommand>
{
    protected override void BeforeQueryStatus(EventArgs e) => ThreadHelper.JoinableTaskFactory.Run(async () =>
                                                                   {
                                                                       Command.Enabled = await Command.IsEnabledForAnySqlProjectAsync();
                                                                   });

    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var project = await VS.Solutions.GetActiveProjectAsync();
        if (project != null)
        {
            await PipelineHandler.AddPipelineAsync(project);
        }
    }
}
