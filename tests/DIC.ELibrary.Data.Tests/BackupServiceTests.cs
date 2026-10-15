using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Data.Tests;

public class BackupServiceTests
{
    [Fact]
    public async Task Backup_CreatesVerifiableFile()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var backup = host.GetService<IBackupService>();
        var target = Path.Combine(host.DataDirectory, "manual", "library.db");

        var result = await backup.BackupAsync(target);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(target));

        var verification = await backup.VerifyAsync(target);
        Assert.True(verification.Succeeded);
        Assert.True(verification.Value!.IsValid);
        Assert.True(verification.Value.UserCount >= 1);
    }

    [Fact]
    public async Task Verify_NonexistentFile_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var backup = host.GetService<IBackupService>();

        var result = await backup.VerifyAsync(Path.Combine(host.DataDirectory, "missing.db"));

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_NonDatabaseFile_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var backup = host.GetService<IBackupService>();
        var bogus = Path.Combine(host.DataDirectory, "bogus.db");
        await File.WriteAllTextAsync(bogus, "this is not a sqlite database");

        var result = await backup.VerifyAsync(bogus);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ListBackups_ReturnsOnlyDatabaseFiles()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var backup = host.GetService<IBackupService>();
        var folder = Path.Combine(host.DataDirectory, "listing");

        await backup.BackupAsync(Path.Combine(folder, "first.db"));
        await backup.BackupAsync(Path.Combine(folder, "second.db"));
        await File.WriteAllTextAsync(Path.Combine(folder, "notes.txt"), "ignore me");

        var files = await backup.ListBackupsAsync(folder);

        Assert.Equal(2, files.Count);
        Assert.All(files, f => Assert.EndsWith(".db", f.FileName));
    }

    [Fact]
    public async Task IsDue_HonoursEnabledAndInterval()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var scheduler = host.GetService<IBackupScheduler>();
        var now = DateTimeOffset.UtcNow;

        Assert.False(scheduler.IsDue(new BackupSchedule { Enabled = false }, now));
        Assert.True(scheduler.IsDue(new BackupSchedule { Enabled = true, IntervalDays = 1 }, now));
        Assert.False(scheduler.IsDue(new BackupSchedule
        {
            Enabled = true,
            IntervalDays = 7,
            LastRunAt = now.AddDays(-3)
        }, now));
        Assert.True(scheduler.IsDue(new BackupSchedule
        {
            Enabled = true,
            IntervalDays = 7,
            LastRunAt = now.AddDays(-8)
        }, now));
    }

    [Fact]
    public async Task RunIfDue_Disabled_DoesNothing()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var host = await TestLibraryHost.CreateAsync(clock: clock);
        var scheduler = host.GetService<IBackupScheduler>();

        await scheduler.SaveScheduleAsync(new BackupSchedule { Enabled = false });

        var result = await scheduler.RunIfDueAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Empty(await host.GetService<IBackupService>().ListBackupsAsync(scheduler.DefaultFolder));
    }

    [Fact]
    public async Task RunIfDue_EnabledFirstTime_CreatesBackupAndRecordsLastRun()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var host = await TestLibraryHost.CreateAsync(clock: clock);
        var scheduler = host.GetService<IBackupScheduler>();

        await scheduler.SaveScheduleAsync(new BackupSchedule { Enabled = true, IntervalDays = 1 });

        var result = await scheduler.RunIfDueAsync();
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.True(File.Exists(result.Value!));

        var schedule = await scheduler.GetScheduleAsync();
        Assert.NotNull(schedule.LastRunAt);
        Assert.Equal(clock.UtcNow, schedule.LastRunAt!.Value);

        // Same instant: not due again.
        var second = await scheduler.RunIfDueAsync();
        Assert.Null(second.Value);
    }

    [Fact]
    public async Task RunIfDue_AfterInterval_IsDueAgain()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var host = await TestLibraryHost.CreateAsync(clock: clock);
        var scheduler = host.GetService<IBackupScheduler>();

        await scheduler.SaveScheduleAsync(new BackupSchedule { Enabled = true, IntervalDays = 1 });
        Assert.NotNull((await scheduler.RunIfDueAsync()).Value);

        clock.Advance(TimeSpan.FromHours(25));
        var result = await scheduler.RunIfDueAsync();
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task RunNow_PrunesOldBackupsToKeepCount()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var host = await TestLibraryHost.CreateAsync(clock: clock);
        var scheduler = host.GetService<IBackupScheduler>();

        await scheduler.SaveScheduleAsync(new BackupSchedule { Enabled = true, IntervalDays = 1, Keep = 2 });

        for (var i = 0; i < 4; i++)
        {
            var result = await scheduler.RunNowAsync();
            Assert.True(result.Succeeded);
            clock.Advance(TimeSpan.FromHours(1));
        }

        var files = await host.GetService<IBackupService>().ListBackupsAsync(scheduler.DefaultFolder);
        Assert.Equal(2, files.Count);
    }
}

internal sealed class FakeClock : IClock
{
    private DateTimeOffset _utcNow;

    public FakeClock(DateTimeOffset utcNow) => _utcNow = utcNow;

    public DateTimeOffset UtcNow => _utcNow;

    public DateTimeOffset LocalNow => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
