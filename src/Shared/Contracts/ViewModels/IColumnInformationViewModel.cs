using System.Collections.ObjectModel;

namespace SqlProjectsPowerTools;

public interface IColumnInformationViewModel : IObjectTreeEditableViewModel, IObjectTreeSelectableViewModel, IViewModel
{
    public ObservableCollection<IColumnChildrenViewModel> Children { get; }

    public bool IsPrimaryKey { get; set; }

    public bool IsForeignKey { get; set; }

    public bool IsColumn { get; }

    public bool IsTableSelected { get; set; }

    public bool IsEnabled { get; }

    public void SetSelected(bool value);
}