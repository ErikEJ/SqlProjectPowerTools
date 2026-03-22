#if !SSMS
using Microsoft.VisualStudio.Data.Services;
#endif

namespace SqlProjectsPowerTools
{
    public interface IVisualStudioAccess
    {
#if !SSMS
        DatabaseConnectionModel PromptForNewDatabaseConnection();

        Task RemoveDatabaseConnectionAsync(IVsDataConnection dataConnection);
#endif

        void ShowMessage(string message);
    }
}
