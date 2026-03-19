namespace SqlProjectsPowerTools;

[Command(PackageIds.cmdidReverseEngineerDab)]
internal sealed class DabCommand : BaseCommand<DabCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        var project = await VS.Solutions.GetActiveProjectAsync();
        if (project != null)
        {
            await DabBuilderHandler.BuildDabConfigAsync(project);
        }
    }
}
