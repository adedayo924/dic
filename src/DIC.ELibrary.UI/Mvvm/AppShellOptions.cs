using DIC.ELibrary.Core;

namespace DIC.ELibrary.UI.Mvvm;

public sealed record NavigationItem(string Title, string Glyph, Type ViewModelType);

/// <summary>Per-application shell configuration (branding and navigation).</summary>
public sealed class AppShellOptions
{
    public string ProductName { get; set; } = AppInfo.Name;
    public string Subtitle { get; set; } = string.Empty;

    /// <summary>Resource key of the header background brush (defined in Themes/Palette.xaml).</summary>
    public string HeaderBrushKey { get; set; } = "IndigoBrush";

    /// <summary>Resource key of the accent brush used for the header chip.</summary>
    public string AccentBrushKey { get; set; } = "RedBrush";

    /// <summary>When true, only Librarian/Administrator roles may sign in (admin tool).</summary>
    public bool RequireStaffRole { get; set; }

    public Type? StartupViewModel { get; set; }

    public List<NavigationItem> NavigationItems { get; } = new();

    /// <summary>Navigation shown to Librarian/Administrator accounts (falls back to <see cref="NavigationItems"/> when empty).</summary>
    public List<NavigationItem> StaffNavigationItems { get; } = new();

    /// <summary>Landing screen for Librarian/Administrator accounts.</summary>
    public Type? StaffStartupViewModel { get; set; }
}
