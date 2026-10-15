using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DIC.ELibrary.UI.Converters;

public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "—";
        }
        double bytes = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        while (bytes >= 1024 && order < units.Length - 1)
        {
            order++;
            bytes /= 1024;
        }
        return $"{bytes:0.##} {units[order]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            System.Collections.ICollection c => c.Count > 0,
            _ => true
        };
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
