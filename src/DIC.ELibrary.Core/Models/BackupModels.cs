namespace DIC.ELibrary.Core.Models;

/// <summary>User-configurable automatic backup policy (persisted in application settings).</summary>
public class BackupSchedule
{
    public bool Enabled { get; set; }

    /// <summary>Minimum number of days between automatic backups.</summary>
    public int IntervalDays { get; set; } = 1;

    /// <summary>Number of most-recent automatic backups to retain.</summary>
    public int Keep { get; set; } = 7;

    /// <summary>Target folder; when empty the default backup folder under the data directory is used.</summary>
    public string? Folder { get; set; }

    /// <summary>When the last automatic backup ran, or <c>null</c> if it has never run.</summary>
    public DateTimeOffset? LastRunAt { get; set; }
}

/// <summary>Metadata about a backup file on disk.</summary>
public class BackupFileInfo
{
    public string Path { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}

/// <summary>Outcome of verifying a backup file's integrity and schema.</summary>
public class BackupVerificationResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
    public int BookCount { get; init; }
    public int UserCount { get; init; }
}
