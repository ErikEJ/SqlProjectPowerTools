using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface IObjectTreeRootItemViewModel : IObjectTreeSelectableViewModel, IViewModel
    {
        bool IsVisible { get; }

        ObservableCollection<ISchemaInformationViewModel> Schemas { get; }

        ObjectTypeIcon ObjectTypeIcon { get; }

        ObjectType ObjectType { get; set; }
    }
}