#if !SSMS
using Microsoft.VisualStudio.Data.Services;
#endif

namespace SqlProjectsPowerTools
{
    public class VisualStudioAccess : IVisualStudioAccess
    {
#if !SSMS
        DatabaseConnectionModel IVisualStudioAccess.PromptForNewDatabaseConnection()
        {
            DatabaseConnectionModel info = null;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                // Switch to main thread
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                info = await VsDataHelper.PromptForInfoAsync();
            });

#pragma warning disable S2583 // Conditionally executed code should be reachable
            if (info == null)
            {
                return null;
            }
#pragma warning restore S2583 // Conditionally executed code should be reachable

            return new DatabaseConnectionModel
            {
                ConnectionName = info.ConnectionName,
                ConnectionString = info.ConnectionString,
                DatabaseType = info.DatabaseType,
                IsFromServerExplorer = info.IsFromServerExplorer,
            };
        }

        async System.Threading.Tasks.Task IVisualStudioAccess.RemoveDatabaseConnectionAsync(IVsDataConnection dataConnection)
        {
            await VsDataHelper.RemoveDataConnectionAsync(dataConnection);
        }
#endif

        void IVisualStudioAccess.ShowMessage(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VSHelper.ShowMessage(message);
        }
    }
}