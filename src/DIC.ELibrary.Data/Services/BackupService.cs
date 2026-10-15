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

            await using var destination = new SqliteConnection($"Data Source={targetFile}");
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);

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
}
