using System.Windows.Input;

namespace SqlProjectsPowerTools;

public interface IObjectTreeSelectableViewModel
{
    public bool? IsSelected { get; }

    public ICommand SetSelectedCommand { get; }
}