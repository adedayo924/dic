using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.UI.ViewModels;

public partial class AdminSettingsViewModel : ViewModelBase
{
    private readonly IKeyManager _keys;
    private readonly IBackupService _backup;
    private readonly IBackupScheduler _scheduler;
    private readonly IAppSettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;

    public AdminSettingsViewModel(
        IKeyManager keys,
        IBackupService backup,
        IBackupScheduler scheduler,
        IAppSettingsService settings,
        IDialogService dialogs,
        IThemeService theme,
        IOptions<LibraryOptions> library)
    {
        _keys = keys;
        _backup = backup;
        _scheduler = scheduler;
        _settings = settings;
        _dialogs = dialogs;
        _theme = theme;
        Title = "Settings";
        DataDirectory = library.Value.ResolvedDataDirectory;
        DatabasePath = library.Value.DatabasePath;
        AvailableThemes = new() { "Light", "Dark" };
        BackupFolder = _scheduler.DefaultFolder;
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    [ObservableProperty]
    private string publicKeyFingerprint = string.Empty;

    [ObservableProperty]
    private string contentKeyFingerprint = string.Empty;

    [ObservableProperty]
    private bool hasSigningKey;

    [ObservableProperty]
    private string libraryName = "DIC E-Library";

    [ObservableProperty]
    private string? statusMessage;

    public List<string> AvailableThemes { get; }

    [ObservableProperty]
    private string selectedTheme = "Light";

    [ObservableProperty]
    private bool backupEnabled;

    [ObservableProperty]
    private string backupIntervalDays = "1";

    [ObservableProperty]
    private string backupKeep = "7";

    [ObservableProperty]
    private string backupFolder = string.Empty;

    [ObservableProperty]
    private string lastBackupText = "No automatic backup yet.";

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        RefreshKeys();
        LibraryName = await _settings.GetAsync("LibraryName") ?? "DIC E-Library";
        var stored = await _settings.GetAsync("Theme");
        var theme = string.IsNullOrWhiteSpace(stored) ? ThemeService.Light : stored;
        SelectedTheme = string.Equals(theme, ThemeService.Dark, StringComparison.OrdinalIgnoreCase)
            ? ThemeService.Dark
            : ThemeService.Light;
        _theme.Apply(SelectedTheme);

        var schedule = await _scheduler.GetScheduleAsync();
        BackupEnabled = schedule.Enabled;
        BackupIntervalDays = schedule.IntervalDays.ToString(CultureInfo.InvariantCulture);
        BackupKeep = schedule.Keep.ToString(CultureInfo.InvariantCulture);
        BackupFolder = string.IsNullOrWhiteSpace(schedule.Folder) ? _scheduler.DefaultFolder : schedule.Folder!;
        LastBackupText = schedule.LastRunAt is { } lastRun
            ? $"Last automatic backup: {lastRun.ToLocalTime():dd MMM yyyy HH:mm}"
            : "No automatic backup yet.";
    });

    partial void OnSelectedThemeChanged(string value)
    {
        var theme = string.Equals(value, ThemeService.Dark, StringComparison.OrdinalIgnoreCase)
            ? ThemeService.Dark
            : ThemeService.Light;
        _theme.Apply(theme);
        _ = _settings.SetAsync("Theme", theme);
    }

    private void RefreshKeys()
    {
        try
        {
            HasSigningKey = _keys.HasSigningKey;
            PublicKeyFingerprint = _keys.PublicKeyFingerprint;
            ContentKeyFingerprint = _keys.ContentKeyFingerprint;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await _settings.SetAsync("LibraryName", LibraryName);
        StatusMessage = "Settings saved.";
    }

    [RelayCommand]
    private void ExportPublicKey()
    {
        var target = _dialogs.SaveFile("Public key (*.pem)|*.pem", "Export public key", "verification-key.pem");
        if (target is null)
        {
            return;
        }
        try
        {
            File.WriteAllText(target, _keys.ExportPublicKeyPem());
            StatusMessage = "Public key exported.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void ImportPublicKey()
    {
        var file = _dialogs.OpenFile("Public key (*.pem)|*.pem|All files (*.*)|*.*", "Import public key");
        if (file is null)
        {
            return;
        }
        try
        {
            _keys.ImportPublicKeyPem(File.ReadAllText(file));
            RefreshKeys();
            StatusMessage = "Public key imported.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void RegenerateKey()
    {
        if (!_dialogs.Confirm(
            "Regenerate the package signing key? Existing packages signed with the old key will no longer verify.",
            "Regenerate key"))
        {
            return;
        }
        try
        {
            _keys.RegenerateSigningKey();
            RefreshKeys();
            StatusMessage = "Signing key regenerated.";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        var target = _dialogs.SaveFile("Database backup (*.db)|*.db", "Backup database", "library-backup.db");
        if (target is null)
        {
            return;
        }
        var result = await _backup.BackupAsync(target);
        if (result.Succeeded)
        {
            StatusMessage = $"Backup saved to {result.Value}.";
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Backup failed.");
        }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var file = _dialogs.OpenFile("Database backup (*.db)|*.db|All files (*.*)|*.*", "Restore database");
        if (file is null)
        {
            return;
        }
        if (!_dialogs.Confirm("Restore will replace the current library data. Continue?", "Restore database"))
        {
            return;
        }
        var result = await _backup.RestoreAsync(file);
        if (result.Succeeded)
        {
            _dialogs.ShowInfo("Database restored. Restart the application to see the changes.", "Restore");
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Restore failed.");
        }
    }

    [RelayCommand]
    private void BrowseBackupFolder()
    {
        var folder = _dialogs.PickFolder("Select backup folder");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            BackupFolder = folder;
        }
    }

    [RelayCommand]
    private async Task SaveBackupScheduleAsync()
    {
        var interval = int.TryParse(BackupIntervalDays, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i > 0 ? i : 1;
        var keep = int.TryParse(BackupKeep, NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) && k > 0 ? k : 7;

        await _scheduler.SaveScheduleAsync(new BackupSchedule
        {
            Enabled = BackupEnabled,
            IntervalDays = interval,
            Keep = keep,
            Folder = string.IsNullOrWhiteSpace(BackupFolder) ? _scheduler.DefaultFolder : BackupFolder.Trim()
        });

        BackupIntervalDays = interval.ToString(CultureInfo.InvariantCulture);
        BackupKeep = keep.ToString(CultureInfo.InvariantCulture);
        StatusMessage = "Backup schedule saved.";
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        var result = await _scheduler.RunNowAsync();
        if (result.Succeeded)
        {
            StatusMessage = $"Backup created: {result.Value}";
            LastBackupText = $"Last automatic backup: {DateTimeOffset.Now:dd MMM yyyy HH:mm}";
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Backup failed.");
        }
    }

    [RelayCommand]
    private async Task VerifyBackupAsync()
    {
        var file = _dialogs.OpenFile("Database backup (*.db)|*.db|All files (*.*)|*.*", "Verify backup");
        if (file is null)
        {
            return;
        }

        var result = await _backup.VerifyAsync(file);
        if (result.Succeeded && result.Value is { } report)
        {
            _dialogs.ShowInfo(
                $"Backup is valid.{Environment.NewLine}{Environment.NewLine}" +
                $"Books: {report.BookCount}{Environment.NewLine}Users: {report.UserCount}",
                "Backup verified");
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Backup could not be verified.");
        }
    }
}
