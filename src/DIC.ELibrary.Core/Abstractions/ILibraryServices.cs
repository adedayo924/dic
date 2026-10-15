using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Abstractions;

public interface ILoanService
{
    Task<OperationResult<LoanSummary>> BorrowAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default);
    Task<OperationResult> ReturnAsync(Guid loanId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PagedResult<LoanSummary>> QueryAsync(LoanStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> RefreshOverdueAsync(CancellationToken cancellationToken = default);
}

public interface IReaderService
{
    Task<IReadOnlyList<BookSummary>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsFavoriteAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default);
    Task ToggleFavoriteAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookmarkSummary>> GetBookmarksAsync(Guid userId, Guid? bookId, CancellationToken cancellationToken = default);
    Task<OperationResult<BookmarkSummary>> AddBookmarkAsync(Guid userId, Guid bookId, int page, string? label, CancellationToken cancellationToken = default);
    Task RemoveBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default);
    Task<ReadingProgress?> GetProgressAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default);
    Task SaveProgressAsync(Guid userId, Guid bookId, int page, double percent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookSummary>> GetHistoryAsync(Guid userId, int count, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task LogAsync(AuditAction action, string? entityType = null, string? entityId = null, string? details = null, CancellationToken cancellationToken = default);
    Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public interface IAppSettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<OperationResult<string>> BackupAsync(string targetFile, CancellationToken cancellationToken = default);
    Task<OperationResult> RestoreAsync(string sourceFile, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    /// <summary>Builds a CSV summary of library statistics (the Reports page).</summary>
    Task<string> BuildStatisticsReportCsvAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds a CSV of all circulation transactions (loans).</summary>
    Task<string> BuildCirculationReportCsvAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds a CSV of the full audit log.</summary>
    Task<string> BuildAuditReportCsvAsync(CancellationToken cancellationToken = default);
}
