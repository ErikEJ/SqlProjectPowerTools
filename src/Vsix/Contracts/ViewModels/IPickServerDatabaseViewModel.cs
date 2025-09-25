using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface IPickServerDatabaseViewModel : IViewModel
    {
        event EventHandler<CloseRequestedEventArgs> CloseRequested;

        ICommand LoadedCommand { get; }

        ICommand AddDatabaseConnectionCommand { get; }

        ICommand AddAdhocDatabaseConnectionCommand { get; }

        ICommand AddDatabaseDefinitionCommand { get; }

        ICommand OkCommand { get; }

        ICommand CancelCommand { get; }

        ObservableCollection<DatabaseConnectionModel> DatabaseConnections { get; }

        ObservableCollection<CodeGenerationItem> CodeGenerationModeList { get; }

        List<SchemaInfo> Schemas { get; }

        DatabaseConnectionModel SelectedDatabaseConnection { get; set; }

        string UiHint { get; set; }

        int CodeGenerationMode { get; set; }

        bool FilterSchemas { get; set; }
    }
}