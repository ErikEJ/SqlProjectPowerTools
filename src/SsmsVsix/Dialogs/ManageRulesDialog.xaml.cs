using System.Diagnostics;
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
            if (e.Uri.Scheme is "http" or "https")
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }

            e.Handled = true;
        }
    }
}
