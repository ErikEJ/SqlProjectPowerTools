using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace SqlProjectsPowerTools
{
    internal static class VSHelper
    {
        public static async Task<bool> IsDebugModeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsDebugger debugger = await VS.Services.GetDebuggerAsync();

            DBGMODE[] mode = new DBGMODE[1];
            ErrorHandler.ThrowOnFailure(debugger.GetMode(mode));

            if (mode[0] != DBGMODE.DBGMODE_Design)
            {
                return true;
            }

            return false;
        }

        public static VSConstants.MessageBoxResult ShowError(string errorText)
        {
            return VS.MessageBox.ShowError("SQL Database Project Power Tools", errorText);
        }

        public static VSConstants.MessageBoxResult ShowMessage(string messageText)
        {
            return VS.MessageBox.Show("SQL Database Project Power Tools", messageText, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK);
        }

        public static VSConstants.MessageBoxResult ShowWarning(string messageText)
        {
            return VS.MessageBox.Show("SQL Database Project Power Tools", messageText, buttons: OLEMSGBUTTON.OLEMSGBUTTON_OK, icon: OLEMSGICON.OLEMSGICON_WARNING);
        }
    }
}