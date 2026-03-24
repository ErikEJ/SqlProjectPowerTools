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
        }

        public (bool ClosedByOK, ManageRulesViewModel ViewModel) ShowAndAwaitUserResponse()
        {
            var closedByOk = ShowModal() == true;
            return (closedByOk, viewModel);
        }
    }
}
