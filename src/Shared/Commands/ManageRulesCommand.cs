using System;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidManageRules)]
    internal sealed class ManageRulesCommand : BaseCommand<ManageRulesCommand>
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
                await ManageRulesHandler.RunAsync(project);
            }
        }
    }
}
