using System.Collections.Generic;
using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface ITableInformationViewModel : IObjectTreeEditableViewModel, IObjectTreeSelectableViewModel, IViewModel
    {
        string Schema { get; set; }

        bool HasPrimaryKey { get; }

        IEnumerable<string> ExcludedIndexes { get; set; }

        ObjectType ObjectType { get; set; }

        ObjectTypeIcon ObjectTypeIcon { get; }

        ObservableCollection<IColumnInformationViewModel> Columns { get; }

        bool IsVisible { get; set; }

        string ModelDisplayName { get; set; }
    }
}