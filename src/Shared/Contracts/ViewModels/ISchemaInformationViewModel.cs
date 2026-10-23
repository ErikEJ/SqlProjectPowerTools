using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools;

public interface ISchemaInformationViewModel : IObjectTreeSelectableViewModel, IViewModel
{
    public string Name { get; set; }

    public bool IsVisible { get; }

    public ObservableCollection<ITableInformationViewModel> Objects { get; }

    public Schema ReplacingSchema { get; set; }
}