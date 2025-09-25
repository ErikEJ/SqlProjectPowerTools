using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public partial class PickServerDatabaseDialog : IPickServerDatabaseDialog
    {
        private readonly Func<(DatabaseConnectionModel Connection, bool FilterSchemas, SchemaInfo[] Schemas, string UiHint)> getDialogResult;
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
            getDialogResult = () => (viewModel.SelectedDatabaseConnection, viewModel.FilterSchemas, viewModel.Schemas.ToArray(), viewModel.UiHint);
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
                foreach (var item in allowedVersions)
                {
                    viewModel.CodeGenerationModeList.Add(item);
                }
            };

            uiHint = uiHint =>
            {
                viewModel.UiHint = uiHint;
            };

            InitializeComponent();
        }

        public (bool ClosedByOK, (DatabaseConnectionModel Connection, bool FilterSchemas, SchemaInfo[] Schemas, string UiHint) Payload) ShowAndAwaitUserResponse(bool modal)
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

        public void PublishCodeGenerationMode(IList<CodeGenerationItem> allowedVersions)
        {
            codeGeneration(allowedVersions);
        }

        public void PublishUiHint(string uiHint)
        {
            this.uiHint(uiHint);
        }

        public (DatabaseConnectionModel Connection, bool FilterSchemas, SchemaInfo[] Schemas, string UiHint) GetResults()
        {
            return getDialogResult();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DatabaseConnectionCombobox.Focus();
        }
    }
}