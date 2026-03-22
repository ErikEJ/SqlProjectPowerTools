using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public partial class PickServerDatabaseDialog : IPickServerDatabaseDialog
    {
        private readonly Func<(DatabaseConnectionModel Connection, int CodegenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions)> getDialogResult;
        private readonly Action<IEnumerable<DatabaseConnectionModel>> addConnections;
        private readonly Action<IEnumerable<DatabaseConnectionModel>> addDefinitions;
        private readonly Action<IEnumerable<SchemaInfo>> addSchemas;
        private readonly Action<IList<CodeGenerationItem>> codeGeneration;
        private readonly Action<string> uiHint;

        public PickServerDatabaseDialog(
            IPickServerDatabaseViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.CloseRequested += (sender, args) =>
            {
                DialogResult = args.DialogResult;
                Close();
            };
            getDialogResult = () => (viewModel.SelectedDatabaseConnection, viewModel.CodeGenerationMode, viewModel.Schemas.ToArray(), viewModel.UiHint, viewModel.FilterSchemas);
            addConnections = models =>
            {
                foreach (var model in models)
                {
                    viewModel.DatabaseConnections.Add(model);
                }
            };
            addDefinitions = models =>
            {
                foreach (var model in models)
                {
                    viewModel.DatabaseConnections.Add(model);
                }
            };
            addSchemas = models =>
            {
                viewModel.FilterSchemas = models.Any();
                foreach (var model in models)
                {
                    viewModel.Schemas.Add(model);
                }
            };
            codeGeneration = (allowedVersions) =>
            {
                if (allowedVersions.Count == 1
                    && allowedVersions[0].Value == "Compare")
                {
                    grdRow1.Height = new GridLength(0);
                    grdRow2.Height = new GridLength(0);
                    FilterSchemas.Content = "Use database as source";
                    viewModel.FilterSchemas = true;
                    return;
                }

                if (allowedVersions.Count == 1
                    && allowedVersions[0].Value == "Seed")
                {
                    grdRow1.Height = new GridLength(0);
                    grdRow2.Height = new GridLength(0);
                    grdRow3.Height = new GridLength(0);
                    return;
                }

                foreach (var item in allowedVersions)
                {
                    viewModel.CodeGenerationModeList.Add(item);
                }

                if (viewModel.CodeGenerationModeList.Any())
                {
                    viewModel.CodeGenerationMode = viewModel.CodeGenerationModeList[0].Key;
                }
            };

            uiHint = uiHint =>
            {
                viewModel.UiHint = uiHint;
            };

            InitializeComponent();
        }

        public (bool ClosedByOK, (DatabaseConnectionModel Connection, int CodeGenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions) Payload) ShowAndAwaitUserResponse(bool modal)
        {
            bool closedByOkay;

            if (modal)
            {
                closedByOkay = ShowModal() == true;
            }
            else
            {
                closedByOkay = ShowDialog() == true;
            }

            return (closedByOkay, getDialogResult());
        }

        void IPickServerDatabaseDialog.PublishConnections(IEnumerable<DatabaseConnectionModel> connections)
        {
            addConnections(connections);
        }

        void IPickServerDatabaseDialog.PublishDefinitions(IEnumerable<DatabaseConnectionModel> definitions)
        {
            addDefinitions(definitions);
        }

        public void PublishSchemas(IEnumerable<SchemaInfo> schemas)
        {
            addSchemas(schemas);
        }

        public void PublishFileGenerationMode(IList<CodeGenerationItem> methods)
        {
            codeGeneration(methods);
        }

        public void PublishUiHint(string uiHint)
        {
            this.uiHint(uiHint);
        }

        public (DatabaseConnectionModel Connection, int CodeGenerationMode, SchemaInfo[] Schemas, string UiHint, bool GetDatabaseOptions) GetResults()
        {
            return getDialogResult();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DatabaseConnectionCombobox.Focus();
        }
    }
}