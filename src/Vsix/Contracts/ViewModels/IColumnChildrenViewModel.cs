using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface IColumnChildrenViewModel : IObjectTreeEditableViewModel, IObjectTreeSelectableViewModel, IViewModel
    {
        string Schema { get; set; }

        bool HasPrimaryKey { get; }

        ObjectType ObjectType { get; set; }

        ObjectTypeIcon ObjectTypeIcon { get; }

        ObservableCollection<IColumnInformationViewModel> Columns { get; }

        bool IsVisible { get; set; }

        string ModelDisplayName { get; set; }
    }
}