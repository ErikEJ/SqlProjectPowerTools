using System.Collections.Generic;
using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface IObjectTreeViewModel : IViewModel
{
    public event EventHandler ObjectSelectionChanged;

    public ObservableCollection<IObjectTreeRootItemViewModel> Types { get; }

    public bool IsInEditMode { get; }

    public void Search(string searchText, SearchMode searchMode);

    public void SetSelectionState(bool value);

    public bool? GetSelectionState();

    public IEnumerable<SerializationTableModel> GetSelectedObjects();

    public IEnumerable<Schema> GetRenamedObjects();

    public void AddObjects(IEnumerable<TableModel> objects, IEnumerable<Schema> customReplacers);

    public void SelectObjects(IEnumerable<SerializationTableModel> objects);
}