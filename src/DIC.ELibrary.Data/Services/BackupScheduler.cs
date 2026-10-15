using System.Globalization;
using System.IO;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Services;

/// <summary>Application-setting keys used by the backup scheduler.</summary>
public static class BackupSettingKeys
{
    public const string Enabled = "Backup.Enabled";
    public const string IntervalDays = "Backup.IntervalDays";
    public const string Keep = "Backup.Keep";
    public const string Folder = "Backup.Folder";
    public const string LastRunAt = "Backup.LastRunAt";
}

/// <summary>Reads/writes the backup policy and performs due or on-demand database backups.</summary>
public sealed class BackupScheduler : IBackupScheduler
{
    private readonly IBackupService _backup;
    private readonly IAppSettingsService _settings;
    private readonly IClock _clock;
    private readonly LibraryOptions _library;
    private readonly ILogger<BackupScheduler> _logger;

    public BackupScheduler(
        IBackupService backup,
        IAppSettingsService settings,
        IClock clock,
        IOptions<LibraryOptions> library,
        ILogger<BackupScheduler> logger)
    {
        _backup = backup;
        _settings = settings;
        _clock = clock;
        _library = library.Value;
        _logger = logger;
    }

    public string DefaultFolder => _library.BackupsPath;

    public async Task<BackupSchedule> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        var enabled = await _settings.GetAsync(BackupSettingKeys.Enabled, cancellationToken);
        var interval = await _settings.GetAsync(BackupSettingKeys.IntervalDays, cancellationToken);
        var keep = await _settings.GetAsync(BackupSettingKeys.Keep, cancellationToken);
        var folder = await _settings.GetAsync(BackupSettingKeys.Folder, cancellationToken);
        var lastRun = await _settings.GetAsync(BackupSettingKeys.LastRunAt, cancellationToken);

        return new BackupSchedule
        {
            Enabled = bool.TryParse(enabled, out var e) && e,
            IntervalDays = int.TryParse(interval, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i > 0 ? i : 1,
            Keep = int.TryParse(keep, NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) && k > 0 ? k : 7,
            Folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder,
            LastRunAt = DateTimeOffset.TryParse(lastRun, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
                ? d
                : null
        };
    }

    public async Task SaveScheduleAsync(BackupSchedule schedule, CancellationToken cancellationToken = default)
    {
        await _settings.SetAsync(BackupSettingKeys.Enabled, schedule.Enabled ? "true" : "false", cancellationToken);
        await _settings.SetAsync(BackupSettingKeys.IntervalDays, Math.Max(1, schedule.IntervalDays).ToString(CultureInfo.InvariantCulture), cancellationToken);
        await _settings.SetAsync(BackupSettingKeys.Keep, Math.Max(1, schedule.Keep).ToString(CultureInfo.InvariantCulture), cancellationToken);
        await _settings.SetAsync(BackupSettingKeys.Folder, string.IsNullOrWhiteSpace(schedule.Folder) ? DefaultFolder : schedule.Folder, cancellationToken);
    }

    public bool IsDue(BackupSchedule schedule, DateTimeOffset now)
    {
        if (!schedule.Enabled)
        {
            return false;
        }
        if (schedule.LastRunAt is not { } lastRun)
        {
            return true;
        }
        return now - lastRun >= TimeSpan.FromDays(Math.Max(1, schedule.IntervalDays));
    }

    public async Task<OperationResult<string?>> RunIfDueAsync(CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleAsync(cancellationToken);
        if (!IsDue(schedule, _clock.UtcNow))
        {
            return OperationResult<string?>.Ok(null);
        }

        var result = await RunNowAsync(cancellationToken);
        return result.Succeeded
            ? OperationResult<string?>.Ok(result.Value)
            : OperationResult<string?>.Fail(result.Error ?? "Backup failed.");
    }

    public async Task<OperationResult<string>> RunNowAsync(CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleAsync(cancellationToken);
        var folder = string.IsNullOrWhiteSpace(schedule.Folder) ? DefaultFolder : schedule.Folder!;
        var target = Path.Combine(folder, $"library-{_clock.LocalNow:yyyyMMdd-HHmmss}.db");

        var backup = await _backup.BackupAsync(target, cancellationToken);
        if (!backup.Succeeded)
        {
            return OperationResult<string>.Fail(backup.Error ?? "Backup failed.");
        }

        await _settings.SetAsync(BackupSettingKeys.LastRunAt, _clock.UtcNow.ToString("o", CultureInfo.InvariantCulture), cancellationToken);
        await PruneAsync(folder, Math.Max(1, schedule.Keep), cancellationToken);
        _logger.LogInformation("Scheduled backup created at {Target}.", target);
        return OperationResult<string>.Ok(target);
    }

    private async Task PruneAsync(string folder, int keep, CancellationToken cancellationToken)
    {
        var backups = await _backup.ListBackupsAsync(folder, cancellationToken);
        foreach (var stale in backups.OrderByDescending(b => b.CreatedAt).Skip(keep))
        {
            try
            {
                File.Delete(stale.Path);
                _logger.LogInformation("Pruned old backup {Path}.", stale.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to prune backup {Path}.", stale.Path);
            }
        }
    }
}
