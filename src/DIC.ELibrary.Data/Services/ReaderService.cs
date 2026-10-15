using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class ReaderService : IReaderService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IClock _clock;

    public ReaderService(IDbContextFactory<LibraryDbContext> factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BookSummary>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var books = await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Include(f => f.Book).ThenInclude(b => b.Category)
            .Select(f => f.Book)
            .ToListAsync(cancellationToken);
        return books.Select(b => b.ToSummary()).ToList();
    }

    public async Task<bool> IsFavoriteAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Favorites.AnyAsync(f => f.UserId == userId && f.BookId == bookId, cancellationToken);
    }

    public async Task ToggleFavoriteAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.BookId == bookId, cancellationToken);
        if (existing is null)
        {
            db.Favorites.Add(new Favorite { UserId = userId, BookId = bookId, CreatedAt = _clock.UtcNow });
        }
        else
        {
            db.Favorites.Remove(existing);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookmarkSummary>> GetBookmarksAsync(Guid userId, Guid? bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var q = db.Bookmarks.AsNoTracking().Include(b => b.Book).Where(b => b.UserId == userId);
        if (bookId is not null)
        {
            q = q.Where(b => b.BookId == bookId);
        }
        var bookmarks = await q.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);
        return bookmarks.Select(b => new BookmarkSummary
        {
            Id = b.Id,
            BookId = b.BookId,
            BookTitle = b.Book?.Title ?? string.Empty,
            Page = b.Page,
            Label = b.Label,
            CreatedAt = b.CreatedAt
        }).ToList();
    }

    public async Task<OperationResult<BookmarkSummary>> AddBookmarkAsync(Guid userId, Guid bookId, int page, string? label, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = new Bookmark
        {
            UserId = userId,
            BookId = bookId,
            Page = page,
            Label = label,
            CreatedAt = _clock.UtcNow
        };
        db.Bookmarks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var title = await db.Books.Where(b => b.Id == bookId).Select(b => b.Title).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return OperationResult<BookmarkSummary>.Ok(new BookmarkSummary
        {
            Id = entity.Id,
            BookId = bookId,
            BookTitle = title,
            Page = page,
            Label = label,
            CreatedAt = entity.CreatedAt
        });
    }

    public async Task RemoveBookmarkAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var bookmark = await db.Bookmarks.FirstOrDefaultAsync(b => b.Id == bookmarkId, cancellationToken);
        if (bookmark is not null)
        {
            db.Bookmarks.Remove(bookmark);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<ReadingProgress?> GetProgressAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.ReadingProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.BookId == bookId, cancellationToken);
    }

    public async Task SaveProgressAsync(Guid userId, Guid bookId, int page, double percent, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var progress = await db.ReadingProgresses.FirstOrDefaultAsync(p => p.UserId == userId && p.BookId == bookId, cancellationToken);
        if (progress is null)
        {
            db.ReadingProgresses.Add(new ReadingProgress
            {
                UserId = userId,
                BookId = bookId,
                LastPage = page,
                Percent = percent,
                UpdatedAt = _clock.UtcNow
            });
        }
        else
        {
            progress.LastPage = page;
            progress.Percent = percent;
            progress.UpdatedAt = _clock.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookSummary>> GetHistoryAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var books = await db.ReadingProgresses.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(Math.Max(1, count))
            .Include(p => p.Book).ThenInclude(b => b.Category)
            .Select(p => p.Book)
            .ToListAsync(cancellationToken);
        return books.Select(b => b.ToSummary()).ToList();
    }
}
