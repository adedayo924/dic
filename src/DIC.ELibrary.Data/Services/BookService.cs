using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Services;

public sealed class BookService : IBookService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IPdfService _pdf;
    private readonly ISearchService _search;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly LibraryOptions _library;
    private readonly OcrOptions _ocr;
    private readonly ILogger<BookService> _logger;

    public BookService(
        IDbContextFactory<LibraryDbContext> factory,
        IPdfService pdf,
        ISearchService search,
        IAuditService audit,
        ICurrentUser currentUser,
        IClock clock,
        IOptions<LibraryOptions> library,
        IOptions<OcrOptions> ocr,
        ILogger<BookService> logger)
    {
        _factory = factory;
        _pdf = pdf;
        _search = search;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
        _library = library.Value;
        _ocr = ocr.Value;
        _logger = logger;
    }

    public async Task<PagedResult<BookSummary>> QueryAsync(BookQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var q = db.Books.AsNoTracking().Include(b => b.Category).AsQueryable();

        if (query.Status is not null)
        {
            q = q.Where(b => b.Status == query.Status);
        }
        if (query.CategoryId is not null)
        {
            q = q.Where(b => b.CategoryId == query.CategoryId);
        }
        if (query.TagId is not null)
        {
            q = q.Where(b => b.Tags.Any(t => t.TagId == query.TagId));
        }
        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            q = q.Where(b => b.Author != null && b.Author.Contains(query.Author));
        }
        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            q = q.Where(b => b.Language == query.Language);
        }
        if (query.Year is not null)
        {
            q = q.Where(b => b.PublicationYear == query.Year);
        }

        var term = query.SearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            if (query.FullText)
            {
                var ids = await SearchIdsAsync(term, cancellationToken);
                q = q.Where(b => ids.Contains(b.Id));
            }
            else
            {
                q = q.Where(b =>
                    b.Title.Contains(term) ||
                    (b.Subtitle != null && b.Subtitle.Contains(term)) ||
                    (b.Author != null && b.Author.Contains(term)) ||
                    (b.Isbn != null && b.Isbn.Contains(term)));
            }
        }

        q = query.SortBy switch
        {
            BookSortField.Author => query.SortDescending ? q.OrderByDescending(b => b.Author) : q.OrderBy(b => b.Author),
            BookSortField.PublicationYear => query.SortDescending ? q.OrderByDescending(b => b.PublicationYear) : q.OrderBy(b => b.PublicationYear),
            BookSortField.RecentlyAdded => query.SortDescending ? q.OrderByDescending(b => b.CreatedAt) : q.OrderBy(b => b.CreatedAt),
            BookSortField.Popularity => query.SortDescending
                ? q.OrderByDescending(b => b.Loans.Count)
                : q.OrderBy(b => b.Loans.Count),
            _ => query.SortDescending ? q.OrderByDescending(b => b.Title) : q.OrderBy(b => b.Title)
        };

        var total = await q.CountAsync(cancellationToken);
        var books = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = books.Select(b => b.ToSummary()).ToList();
        return new PagedResult<BookSummary> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    private async Task<List<Guid>> SearchIdsAsync(string term, CancellationToken cancellationToken)
    {
        var result = await _search.SearchAsync(term, 1, 500, cancellationToken);
        return result.Items.Select(i => i.BookId).ToList();
    }

    public async Task<BookDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.Tags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return book?.ToDetail();
    }

    public async Task<OperationResult<BookDetail>> CreateAsync(BookEditModel model, string? pdfSourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return OperationResult<BookDetail>.Fail("Title is required.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = new Book
        {
            Title = model.Title.Trim(),
            Subtitle = model.Subtitle,
            Author = model.Author,
            Publisher = model.Publisher,
            PublicationYear = model.PublicationYear,
            Isbn = model.Isbn,
            Language = model.Language,
            Description = model.Description,
            CategoryId = model.CategoryId,
            Status = model.Status,
            CreatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow,
            CreatedByUserId = _currentUser.UserId
        };

        if (!string.IsNullOrWhiteSpace(pdfSourcePath) && File.Exists(pdfSourcePath))
        {
            await ImportPdfAsync(book, pdfSourcePath, cancellationToken);
        }

        await SyncTagsAsync(db, book, model.Tags, cancellationToken);
        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);

        await _search.ReindexBookAsync(book.Id, cancellationToken);
        await _audit.LogAsync(AuditAction.BookCreated, nameof(Book), book.Id.ToString(), book.Title, cancellationToken);

        var detail = await GetAsync(book.Id, cancellationToken);
        return OperationResult<BookDetail>.Ok(detail!);
    }

    public async Task<OperationResult> UpdateAsync(BookEditModel model, CancellationToken cancellationToken = default)
    {
        if (model.Id is null)
        {
            return OperationResult.Fail("Book id is required.");
        }
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return OperationResult.Fail("Title is required.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.Include(b => b.Tags).FirstOrDefaultAsync(b => b.Id == model.Id, cancellationToken);
        if (book is null)
        {
            return OperationResult.Fail("Book not found.");
        }

        book.Title = model.Title.Trim();
        book.Subtitle = model.Subtitle;
        book.Author = model.Author;
        book.Publisher = model.Publisher;
        book.PublicationYear = model.PublicationYear;
        book.Isbn = model.Isbn;
        book.Language = model.Language;
        book.Description = model.Description;
        book.CategoryId = model.CategoryId;
        book.Status = model.Status;
        book.UpdatedAt = _clock.UtcNow;

        book.Tags.Clear();
        await SyncTagsAsync(db, book, model.Tags, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await _search.ReindexBookAsync(book.Id, cancellationToken);
        await _audit.LogAsync(AuditAction.BookUpdated, nameof(Book), book.Id.ToString(), book.Title, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (book is null)
        {
            return OperationResult.Fail("Book not found.");
        }

        db.Books.Remove(book);
        await db.SaveChangesAsync(cancellationToken);

        var dir = _library.BookDirectory(id);
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to delete book directory {Directory}.", dir);
            }
        }

        await _search.ReindexBookAsync(id, cancellationToken);
        await _audit.LogAsync(AuditAction.BookDeleted, nameof(Book), id.ToString(), book.Title, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetStatusAsync(Guid id, BookStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (book is null)
        {
            return OperationResult.Fail("Book not found.");
        }
        book.Status = status;
        book.UpdatedAt = _clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.BookUpdated, nameof(Book), id.ToString(), $"Status -> {status}", cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<IReadOnlyList<BookSummary>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var books = await db.Books.AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.Status == BookStatus.Published)
            .OrderByDescending(b => b.CreatedAt)
            .Take(Math.Max(1, count))
            .ToListAsync(cancellationToken);
        return books.Select(b => b.ToSummary()).ToList();
    }

    public async Task<IReadOnlyList<string>> GetAuthorsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Books.AsNoTracking()
            .Where(b => b.Author != null && b.Author != "")
            .Select(b => b.Author!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Books.AsNoTracking()
            .Where(b => b.Language != null && b.Language != "")
            .Select(b => b.Language!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> ImportMetadataAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
        });

        var records = csv.GetRecords<BookCsvRow>().ToList();
        var created = 0;
        foreach (var row in records)
        {
            if (string.IsNullOrWhiteSpace(row.Title))
            {
                continue;
            }
            db.Books.Add(new Book
            {
                Title = row.Title.Trim(),
                Subtitle = row.Subtitle,
                Author = row.Author,
                Publisher = row.Publisher,
                PublicationYear = row.Year,
                Isbn = row.Isbn,
                Language = row.Language,
                Description = row.Description,
                Status = BookStatus.Published,
                CreatedAt = _clock.UtcNow,
                UpdatedAt = _clock.UtcNow,
                CreatedByUserId = _currentUser.UserId
            });
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await _search.RebuildIndexAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.BookImported, nameof(Book), null, $"Imported {created} rows", cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ApplyOcrTextAsync(Guid bookId, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return OperationResult.Fail("No text was extracted.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);
        if (book is null)
        {
            return OperationResult.Fail("Book not found.");
        }

        var dir = _library.BookDirectory(bookId);
        Directory.CreateDirectory(dir);
        var textPath = Path.Combine(dir, "content.txt");
        await File.WriteAllTextAsync(textPath, text, cancellationToken);

        book.TextPath = _library.ToRelative(textPath);
        book.OcrStatus = OcrStatus.Completed;
        book.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await _search.ReindexBookAsync(bookId, cancellationToken);
        await _audit.LogAsync(AuditAction.OcrRun, nameof(Book), bookId.ToString(), "OCR completed", cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetCoverAsync(Guid bookId, byte[] coverPng, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);
        if (book is null)
        {
            return OperationResult.Fail("Book not found.");
        }

        var dir = _library.BookDirectory(bookId);
        Directory.CreateDirectory(dir);
        var coverPath = Path.Combine(dir, "cover.png");
        await File.WriteAllBytesAsync(coverPath, coverPng, cancellationToken);

        book.CoverPath = _library.ToRelative(coverPath);
        book.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await _search.ReindexBookAsync(bookId, cancellationToken);
        return OperationResult.Ok();
    }

    private async Task ImportPdfAsync(Book book, string pdfSourcePath, CancellationToken cancellationToken)
    {
        var dir = _library.BookDirectory(book.Id);
        Directory.CreateDirectory(dir);
        var target = Path.Combine(dir, "book.pdf");
        File.Copy(pdfSourcePath, target, overwrite: true);

        book.FilePath = _library.ToRelative(target);
        book.FileSizeBytes = new FileInfo(target).Length;

        try
        {
            var info = await _pdf.ReadInfoAsync(target, cancellationToken);
            book.PageCount = info.PageCount;
            if (string.IsNullOrWhiteSpace(book.Title) && !string.IsNullOrWhiteSpace(info.Title))
            {
                book.Title = info.Title!;
            }
            if (string.IsNullOrWhiteSpace(book.Author) && !string.IsNullOrWhiteSpace(info.Author))
            {
                book.Author = info.Author;
            }

            book.IsScanned = info.ExtractedTextLength < _ocr.TextDetectionThreshold;
            book.OcrStatus = book.IsScanned ? OcrStatus.Pending : OcrStatus.NotApplicable;

            if (!book.IsScanned)
            {
                var text = await _pdf.ExtractTextAsync(target, cancellationToken);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var textPath = Path.Combine(dir, "content.txt");
                    await File.WriteAllTextAsync(textPath, text, cancellationToken);
                    book.TextPath = _library.ToRelative(textPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read PDF metadata for {Path}.", target);
        }
    }

    private async Task SyncTagsAsync(LibraryDbContext db, Book book, IEnumerable<string> tags, CancellationToken cancellationToken)
    {
        var names = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in names)
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Name = name };
                db.Tags.Add(tag);
            }
            book.Tags.Add(new BookTag { Book = book, Tag = tag });
        }
    }
}

public class BookCsvRow
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public int? Year { get; set; }
    public string? Isbn { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }
}
