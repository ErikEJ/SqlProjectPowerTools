using Microsoft.VisualStudio.Data.Services;

namespace SqlProjectsPowerTools
{
    public interface IVisualStudioAccess
    {
        DatabaseConnectionModel PromptForNewDatabaseConnection();

        Task RemoveDatabaseConnectionAsync(IVsDataConnection dataConnection);

        void ShowMessage(string message);

        Task StartStatusBarAnimationAsync();

        Task StopStatusBarAnimationAsync();

        Task SetStatusBarTextAsync(string text);

        void ShowError(string error);
    }
}