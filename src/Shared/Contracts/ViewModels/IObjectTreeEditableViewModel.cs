using System.Windows.Input;

namespace SqlProjectsPowerTools;

public interface IObjectTreeEditableViewModel
{
    public string Name { get; set; }

    public string NewName { get; set; }

    public string DisplayName { get; }

    public bool IsEditing { get; set; }

    public ICommand StartEditCommand { get; }

    public ICommand ConfirmEditCommand { get; }

    public ICommand CancelEditCommand { get; }
}