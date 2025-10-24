using System.Linq;

namespace SqlProjectsPowerTools
{
    [Command(PackageIds.cmdidImport)]
    internal sealed class ImportCommand : BaseCommand<ImportCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                var isSdkSqlProject = await Command.IsEnabledForModernSqlProjectAsync();
                var hasItems = false;

                var project = await VS.Solutions.GetActiveProjectAsync();
                if (project != null)
                {
                    hasItems = project.Children
                        .Any(c => c.Type == SolutionItemType.PhysicalFile);
                }

                Command.Enabled = isSdkSqlProject && !hasItems;
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var project = await VS.Solutions.GetActiveProjectAsync();

            if (project != null)
            {
                await new ImportHandler().GenerateAsync(project);
            }
        }
    }
}
