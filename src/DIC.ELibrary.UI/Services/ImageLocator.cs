using System.IO;
using System.Windows.Media.Imaging;
using DIC.ELibrary.Core.Options;

namespace DIC.ELibrary.UI.Services;

/// <summary>Resolves stored relative asset paths and loads them as WPF images.</summary>
public static class ImageLocator
{
    private static LibraryOptions _options = new();

    public static void Initialize(LibraryOptions options) => _options = options;

    public static string ResolveAbsolute(string? relativePath)
        => string.IsNullOrWhiteSpace(relativePath) ? string.Empty : _options.ToAbsolute(relativePath);

    public static BitmapImage? Load(string? relativePath)
    {
        var absolute = ResolveAbsolute(relativePath);
        if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
        {
            return null;
        }
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(absolute, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapImage? FromBytes(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
