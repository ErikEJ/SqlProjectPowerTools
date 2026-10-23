using System.Collections.Generic;
using System.Windows.Input;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface IPickTablesViewModel : IViewModel
{
    public event EventHandler<CloseRequestedEventArgs> CloseRequested;

    public ICommand OkCommand { get; }

    public ICommand CancelCommand { get; }

    public bool? TableSelectionThreeState { get; set; }

    public string SearchText { get; set; }

    public SearchMode SearchMode { get; set; }

    public IObjectTreeViewModel ObjectTree { get; }

    public void AddObjects(IEnumerable<TableModel> objects, IEnumerable<Schema> customReplacers);

    public void SelectObjects(IEnumerable<SerializationTableModel> objects);

    public SerializationTableModel[] GetSelectedObjects();

    public Schema[] GetRenamedObjects();
}