using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private readonly IAppSettingsService _settings;

    public SettingsViewModel(
        IAuthService auth,
        ICurrentUser currentUser,
        IDialogService dialogs,
        IThemeService theme,
        IAppSettingsService settings,
        IOptions<LibraryOptions> library)
    {
        _auth = auth;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _theme = theme;
        _settings = settings;
        Title = "Settings";
        DataDirectory = library.Value.ResolvedDataDirectory;
        AvailableThemes = new() { "Light", "Dark" };
    }

    public string DataDirectory { get; }

    /// <summary>Library storage paths are only shown to staff; hidden from students and teachers.</summary>
    public bool ShowLibraryData =>
        _currentUser.IsInRole(UserRole.Librarian) || _currentUser.IsInRole(UserRole.Administrator);

    public string AppVersion => AppInfo.Version;

    public string AccountName => _currentUser.Session?.DisplayName ?? "Guest";

    public string AccountRole => _currentUser.Session?.Role.ToString() ?? string.Empty;

    [ObservableProperty]
    private string currentPassword = string.Empty;

    [ObservableProperty]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private string? passwordMessage;

    public List<string> AvailableThemes { get; }

    [ObservableProperty]
    private string selectedTheme = "Light";

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        PasswordMessage = null;

        var stored = await _settings.GetAsync("Theme");
        var theme = string.IsNullOrWhiteSpace(stored) ? ThemeService.Light : stored;
        SelectedTheme = string.Equals(theme, ThemeService.Dark, StringComparison.OrdinalIgnoreCase)
            ? ThemeService.Dark
            : ThemeService.Light;
        _theme.Apply(SelectedTheme);
    });

    partial void OnSelectedThemeChanged(string value)
    {
        var theme = string.Equals(value, ThemeService.Dark, StringComparison.OrdinalIgnoreCase)
            ? ThemeService.Dark
            : ThemeService.Light;
        _theme.Apply(theme);
        _ = _settings.SetAsync("Theme", theme);
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        PasswordMessage = null;

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            PasswordMessage = "New password must be at least 6 characters.";
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            PasswordMessage = "The new passwords do not match.";
            return;
        }
        if (_currentUser.UserId is not { } userId)
        {
            return;
        }

        var result = await _auth.ChangePasswordAsync(userId, CurrentPassword, NewPassword);
        if (result.Succeeded)
        {
            PasswordMessage = "Password changed successfully.";
            CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to change password.");
        }
    }
}
