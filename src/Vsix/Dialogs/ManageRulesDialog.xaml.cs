using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace SqlProjectsPowerTools
{
    public partial class ManageRulesDialog
    {
        private readonly ManageRulesViewModel viewModel;

        public ManageRulesDialog(ManageRulesViewModel viewModel, string projectName)
        {
            this.viewModel = viewModel;
            DataContext = viewModel;
            viewModel.CloseRequested += (sender, args) =>
            {
                DialogResult = args.DialogResult;
                Close();
            };

            viewModel.ConfirmReset = () =>
                VS.MessageBox.Show(
                    "Reset Rules",
                    "This will enable all rules and set all severities to Warning. Are you sure?",
                    OLEMSGICON.OLEMSGICON_QUERY,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESNO) == VSConstants.MessageBoxResult.IDYES;

            Title = $"Code Analysis Rules - {projectName}";

            InitializeComponent();

            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));
        }

        public (bool ClosedByOK, ManageRulesViewModel ViewModel) ShowAndAwaitUserResponse()
        {
            var closedByOk = ShowModal() == true;
            return (closedByOk, viewModel);
        }

        private static void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            try
            {
                var uri = e.Uri;
                if (uri != null && uri.Scheme is "http" or "https")
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("Failed to navigate to hyperlink: " + ex);
            }
            finally
            {
                e.Handled = true;
            }
        }
    }
}
