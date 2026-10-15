using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Abstractions;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Category>> CreateAsync(string name, string? description, Guid? parentId, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(Guid id, string name, string? description, Guid? parentId, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IBookService
{
    Task<PagedResult<BookSummary>> QueryAsync(BookQuery query, CancellationToken cancellationToken = default);
    Task<BookDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<BookDetail>> CreateAsync(BookEditModel model, string? pdfSourcePath, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(BookEditModel model, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult> SetStatusAsync(Guid id, BookStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAuthorsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetLanguagesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ImportMetadataAsync(Stream csvStream, CancellationToken cancellationToken = default);

    /// <summary>Persists OCR-extracted text for a book and marks OCR complete.</summary>
    Task<OperationResult> ApplyOcrTextAsync(Guid bookId, string text, CancellationToken cancellationToken = default);

    /// <summary>Stores a generated cover image for a book.</summary>
    Task<OperationResult> SetCoverAsync(Guid bookId, byte[] coverPng, CancellationToken cancellationToken = default);
}

public interface ISearchService
{
    Task EnsureIndexAsync(CancellationToken cancellationToken = default);
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
    Task ReindexBookAsync(Guid bookId, CancellationToken cancellationToken = default);
    Task<PagedResult<SearchHit>> SearchAsync(string term, int page, int pageSize, CancellationToken cancellationToken = default);
}

public interface IStatsService
{
    Task<LibraryStats> GetStatsAsync(CancellationToken cancellationToken = default);
}
