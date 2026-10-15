using System.Windows;

namespace DIC.ELibrary.UI.Services;

/// <summary>Swaps the application's neutral colour palette at runtime (Light/Dark).</summary>
public interface IThemeService
{
    string CurrentTheme { get; }
    bool IsDark { get; }
    void Apply(string? themeName);
}

public sealed class ThemeService : IThemeService
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    private const string ThemeFragment = "Themes/Theme.";

    public string CurrentTheme { get; private set; } = Light;

    public bool IsDark => string.Equals(CurrentTheme, Dark, StringComparison.OrdinalIgnoreCase);

    public void Apply(string? themeName)
    {
        var normalized = string.Equals(themeName, Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Light;
        CurrentTheme = normalized;

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var uri = new Uri(
            $"pack://application:,,,/DIC.ELibrary.UI;component/Themes/Theme.{normalized}.xaml",
            UriKind.Absolute);

        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source is not null &&
            d.Source.OriginalString.Contains(ThemeFragment, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Source = uri;
        }
        else
        {
            merged.Insert(0, new ResourceDictionary { Source = uri });
        }
    }
}
