using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SqlProjectsPowerTools
{
    public class ObjectTypeIconToImageStyleConverter : IValueConverter
    {
        public ResourceDictionary ResourceDictionary { get; set; }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var objectType = (ObjectTypeIcon)value;
            var key = $"{objectType}ImageStyle";
            return ResourceDictionary[key];
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}