using System.Globalization;
using System.Windows.Data;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.Converters;

/// <summary>Loads a stored cover path into an image source.</summary>
public sealed class PathToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string path ? ImageLocator.Load(path) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
