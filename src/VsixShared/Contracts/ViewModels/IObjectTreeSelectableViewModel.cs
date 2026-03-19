using System.Windows.Input;

namespace SqlProjectsPowerTools
{
    public interface IObjectTreeSelectableViewModel
    {
        bool? IsSelected { get; }

        ICommand SetSelectedCommand { get; }
    }
}