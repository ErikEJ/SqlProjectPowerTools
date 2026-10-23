using System.Collections.Generic;
using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface ITableInformationViewModel : IObjectTreeEditableViewModel, IObjectTreeSelectableViewModel, IViewModel
{
    public string Schema { get; set; }

    public bool HasPrimaryKey { get; }

    public IEnumerable<string> ExcludedIndexes { get; set; }

    public ObjectType ObjectType { get; set; }

    public ObjectTypeIcon ObjectTypeIcon { get; }

    public ObservableCollection<IColumnInformationViewModel> Columns { get; }

    public bool IsVisible { get; set; }

    public string ModelDisplayName { get; set; }
}