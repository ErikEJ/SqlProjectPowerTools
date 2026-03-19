using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DacFXToolLib.Common;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace SqlProjectsPowerTools
{
    public class PickServerDatabaseViewModel : ViewModelBase, IPickServerDatabaseViewModel
    {
        private readonly IVisualStudioAccess visualStudioAccess;

        private DatabaseConnectionModel selectedDatabaseConnection;
        private bool filterSchemas = false;
        private string uiHint;
        private int codeGenerationMode;

        public PickServerDatabaseViewModel(
            IVisualStudioAccess visualStudioAccess)
        {
            this.visualStudioAccess = visualStudioAccess ?? throw new ArgumentNullException(nameof(visualStudioAccess));

            LoadedCommand = new RelayCommand(Loaded_Executed);
            AddDatabaseConnectionCommand = new RelayCommand(AddDatabaseConnection_Executed);
            RemoveDatabaseConnectionCommand = new RelayCommand(RemoveDatabaseConnection_Executed, RemoveDatabaseConnection_CanExecute);
            OkCommand = new RelayCommand(Ok_Executed, Ok_CanExecute);
            CancelCommand = new RelayCommand(Cancel_Executed);

            CodeGenerationModeList = new ObservableCollection<CodeGenerationItem>();

            DatabaseConnections = new ObservableCollection<DatabaseConnectionModel>();
            Schemas = new List<SchemaInfo>();
            DatabaseConnections.CollectionChanged += (sender, args) => RaisePropertyChanged(nameof(DatabaseConnections));
        }

        public event EventHandler<CloseRequestedEventArgs> CloseRequested;

        public ICommand LoadedCommand { get; }

        public ICommand AddDatabaseConnectionCommand { get; }

        public ICommand AddAdhocDatabaseConnectionCommand { get; }

        public ICommand AddDatabaseDefinitionCommand { get; }

        public ICommand RemoveDatabaseConnectionCommand { get; }

        public ICommand OkCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand FilterSchemasCommand { get; }

        public ObservableCollection<DatabaseConnectionModel> DatabaseConnections { get; }

        public ObservableCollection<CodeGenerationItem> CodeGenerationModeList { get; }

        public List<SchemaInfo> Schemas { get; private set; }

        public int CodeGenerationMode
        {
            get => codeGenerationMode;
            set
            {
                if (value == codeGenerationMode)
                {
                    return;
                }

                codeGenerationMode = value;
                RaisePropertyChanged();
            }
        }

        public bool FilterSchemas
        {
            get => filterSchemas;
            set
            {
                if (value == filterSchemas)
                {
                    return;
                }

                filterSchemas = value;
                RaisePropertyChanged();
            }
        }

        public DatabaseConnectionModel SelectedDatabaseConnection
        {
            get => selectedDatabaseConnection;
            set
            {
                if (Equals(value, selectedDatabaseConnection))
                {
                    return;
                }

                selectedDatabaseConnection = value;
                RaisePropertyChanged();
            }
        }

        public string UiHint
        {
            get
            {
                if (SelectedDatabaseConnection != null)
                {
                    return SelectedDatabaseConnection.ConnectionName ?? selectedDatabaseConnection.FilePath;
                }

                return uiHint;
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var databaseConnectionCandidate = DatabaseConnections
                        .FirstOrDefault(c => c.ConnectionName != null && c.ConnectionName.Equals(value, StringComparison.InvariantCultureIgnoreCase));

                    if (databaseConnectionCandidate != null)
                    {
                        SelectedDatabaseConnection = databaseConnectionCandidate;
                    }

                    databaseConnectionCandidate = DatabaseConnections
                        .FirstOrDefault(c => c.FilePath != null && c.FilePath.Equals(value, StringComparison.InvariantCultureIgnoreCase));

                    if (databaseConnectionCandidate != null)
                    {
                        SelectedDatabaseConnection = databaseConnectionCandidate;
                    }
                }

                uiHint = value;
            }
        }

        private void Loaded_Executed()
        {
            // Database connection first
            var candidate = DatabaseConnections.FirstOrDefault(m =>
                    m.FilePath == null
                    && !string.IsNullOrEmpty(UiHint)
                    && SelectedDatabaseConnection == null
                    && m.ConnectionName.Equals(UiHint, StringComparison.OrdinalIgnoreCase));

            if (candidate != null)
            {
                SelectedDatabaseConnection = candidate;
                return;
            }

            // Database definitions (SQL project) second
            candidate = DatabaseConnections.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.FilePath)
                && !string.IsNullOrEmpty(UiHint)
                && SelectedDatabaseConnection == null
                && c.FilePath.Equals(UiHint, StringComparison.OrdinalIgnoreCase));

            if (candidate != null)
            {
                SelectedDatabaseConnection = candidate;
            }
        }

        private void AddDatabaseConnection_Executed()
        {
            DatabaseConnectionModel newDatabaseConnection;
            try
            {
                newDatabaseConnection = visualStudioAccess.PromptForNewDatabaseConnection();
            }
            catch (Exception e)
            {
                visualStudioAccess.ShowMessage($"Unable to add connection: {e.Message}");
                return;
            }

            if (newDatabaseConnection == null)
            {
                return;
            }

            DatabaseConnections.Add(newDatabaseConnection);
            SelectedDatabaseConnection = newDatabaseConnection;
        }

        private void RemoveDatabaseConnection_Executed()
        {
            if (SelectedDatabaseConnection == null)
            {
                return;
            }

            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await visualStudioAccess.RemoveDatabaseConnectionAsync(SelectedDatabaseConnection.DataConnection);
                });

                DatabaseConnections.Remove(SelectedDatabaseConnection);
            }
            catch (Exception e)
            {
                visualStudioAccess.ShowMessage($"Unable to remove connection: {e.Message}");
                return;
            }

            SelectedDatabaseConnection = null;
        }

        private void Ok_Executed()
        {
            CloseRequested?.Invoke(this, new CloseRequestedEventArgs(true));
        }

        private bool Ok_CanExecute() => SelectedDatabaseConnection != null;

        private bool RemoveDatabaseConnection_CanExecute() => SelectedDatabaseConnection != null && SelectedDatabaseConnection.FilePath == null;

        private void Cancel_Executed()
        {
            SelectedDatabaseConnection = null;
            CloseRequested?.Invoke(this, new CloseRequestedEventArgs(false));
        }
    }
}