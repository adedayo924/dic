using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Services;

public sealed class BackupService : IBackupService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly LibraryOptions _library;
    private readonly IAuditService _audit;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IDbContextFactory<LibraryDbContext> factory,
        IOptions<LibraryOptions> library,
        IAuditService audit,
        ILogger<BackupService> logger)
    {
        _factory = factory;
        _library = library.Value;
        _audit = audit;
        _logger = logger;
    }

    public async Task<OperationResult<string>> BackupAsync(string targetFile, CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(targetFile));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var source = (SqliteConnection)db.Database.GetDbConnection();
            await source.OpenAsync(cancellationToken);

            await using var destination = new SqliteConnection($"Data Source={targetFile};Pooling=False");
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
            SqliteConnection.ClearPool(destination);

            await _audit.LogAsync(AuditAction.BackupCreated, "Database", null, targetFile, cancellationToken);
            _logger.LogInformation("Database backed up to {Target}.", targetFile);
            return OperationResult<string>.Ok(targetFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed.");
            return OperationResult<string>.Fail(ex.Message);
        }
    }

    public async Task<OperationResult> RestoreAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(sourceFile))
            {
                return OperationResult.Fail("Backup file not found.");
            }

            SqliteConnection.ClearAllPools();
            var dbPath = _library.DatabasePath;

            await Task.Run(() =>
            {
                File.Copy(sourceFile, dbPath, overwrite: true);
                foreach (var sidecar in new[] { dbPath + "-wal", dbPath + "-shm" })
                {
                    if (File.Exists(sidecar))
                    {
                        File.Delete(sidecar);
                    }
                }
            }, cancellationToken);

            await _audit.LogAsync(AuditAction.RestorePerformed, "Database", null, sourceFile, cancellationToken);
            _logger.LogInformation("Database restored from {Source}.", sourceFile);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed.");
            return OperationResult.Fail(ex.Message);
        }
    }

    public async Task<OperationResult<BackupVerificationResult>> VerifyAsync(string file, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(file))
        {
            return OperationResult<BackupVerificationResult>.Fail("Backup file not found.");
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(cancellationToken);

            await using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var status = (await integrity.ExecuteScalarAsync(cancellationToken))?.ToString();
                if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<BackupVerificationResult>.Fail($"Integrity check failed: {status}");
                }
            }

            var books = await CountAsync(connection, "Books", cancellationToken);
            var users = await CountAsync(connection, "Users", cancellationToken);

            return OperationResult<BackupVerificationResult>.Ok(new BackupVerificationResult
            {
                IsValid = true,
                Message = "Backup is valid.",
                BookCount = books,
                UserCount = users
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup verification failed for {File}.", file);
            return OperationResult<BackupVerificationResult>.Fail($"Not a valid library backup: {ex.Message}");
        }
    }

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(string folder, CancellationToken cancellationToken = default)
    {
        var backups = new List<BackupFileInfo>();
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.db"))
            {
                try
                {
                    var info = new FileInfo(file);
                    backups.Add(new BackupFileInfo
                    {
                        Path = info.FullName,
                        FileName = info.Name,
                        CreatedAt = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                        SizeBytes = info.Length
                    });
                }
                catch (IOException)
                {
                    // Skip files that disappear mid-enumeration.
                }
            }
        }

        return Task.FromResult<IReadOnlyList<BackupFileInfo>>(
            backups.OrderByDescending(b => b.CreatedAt).ToList());
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }
}
