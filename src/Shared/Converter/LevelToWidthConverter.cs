using System.Globalization;
using System.Windows.Data;

namespace SqlProjectsPowerTools;

public sealed class LevelToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (int)value * 20;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}