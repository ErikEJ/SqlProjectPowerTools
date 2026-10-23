using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface IObjectTreeRootItemViewModel : IObjectTreeSelectableViewModel, IViewModel
{
    public bool IsVisible { get; }

    public ObservableCollection<ISchemaInformationViewModel> Schemas { get; }

    public ObjectTypeIcon ObjectTypeIcon { get; }

    public ObjectType ObjectType { get; set; }
}