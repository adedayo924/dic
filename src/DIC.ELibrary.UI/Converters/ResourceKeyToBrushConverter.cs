using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DIC.ELibrary.UI.Converters;

/// <summary>Resolves a resource key string to a Brush from the application resources.</summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        return Application.Current?.TryFindResource(key) as Brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
