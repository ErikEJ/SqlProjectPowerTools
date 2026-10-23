using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface IPickServerDatabaseViewModel : IViewModel
{
    public event EventHandler<CloseRequestedEventArgs> CloseRequested;

    public ICommand LoadedCommand { get; }

    public ICommand AddDatabaseConnectionCommand { get; }

    public ICommand AddAdhocDatabaseConnectionCommand { get; }

    public ICommand AddDatabaseDefinitionCommand { get; }

    public ICommand OkCommand { get; }

    public ICommand CancelCommand { get; }

    public ObservableCollection<DatabaseConnectionModel> DatabaseConnections { get; }

    public ObservableCollection<CodeGenerationItem> CodeGenerationModeList { get; }

    public IList<SchemaInfo> Schemas { get; }

    public DatabaseConnectionModel SelectedDatabaseConnection { get; set; }

    public string UiHint { get; set; }

    public int CodeGenerationMode { get; set; }

    public bool FilterSchemas { get; set; }
}