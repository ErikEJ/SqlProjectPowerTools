using System.Collections.ObjectModel;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public interface ISchemaInformationViewModel : IObjectTreeSelectableViewModel, IViewModel
    {
        string Name { get; set; }

        bool IsVisible { get; }

        ObservableCollection<ITableInformationViewModel> Objects { get; }

        Schema ReplacingSchema { get; set; }
    }
}