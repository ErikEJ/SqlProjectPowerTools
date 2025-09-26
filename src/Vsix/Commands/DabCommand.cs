namespace SqlProjectsPowerTools;

[Command(PackageIds.cmdidReverseEngineerDab)]
internal sealed class DabCommand : BaseCommand<DabCommand>
{
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await VS.MessageBox.ShowWarningAsync("SQL Database Project Power Tools", "Coming soon");
    }
}
