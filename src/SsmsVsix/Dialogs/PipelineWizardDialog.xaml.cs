using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace SqlProjectsPowerTools
{
    public partial class PipelineWizardDialog
    {
        public PipelineWizardDialog(PipelineWizardViewModel viewModel, string projectName)
        {
            DataContext = viewModel;
            viewModel.CloseRequested += (sender, args) =>
            {
                DialogResult = args.DialogResult;
                Close();
            };

            Title = $"Add Azure DevOps Pipeline - {projectName}";

            InitializeComponent();

            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));
        }

        public bool ShowAndAwaitUserResponse()
        {
            return ShowModal() == true;
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
