using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class StatsService : IStatsService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;

    public StatsService(IDbContextFactory<LibraryDbContext> factory) => _factory = factory;

    public async Task<LibraryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var totalBooks = await db.Books.CountAsync(cancellationToken);
        var published = await db.Books.CountAsync(b => b.Status == BookStatus.Published, cancellationToken);
        var draft = await db.Books.CountAsync(b => b.Status == BookStatus.Draft, cancellationToken);
        var totalUsers = await db.Users.CountAsync(cancellationToken);
        var activeLoans = await db.Loans.CountAsync(l => l.Status == LoanStatus.Active, cancellationToken);
        var overdueLoans = await db.Loans.CountAsync(l => l.Status == LoanStatus.Overdue, cancellationToken);
        var totalCategories = await db.Categories.CountAsync(cancellationToken);
        var totalStorage = await db.Books.SumAsync(b => (long?)b.FileSizeBytes, cancellationToken) ?? 0;

        var byCategory = await db.Categories.AsNoTracking()
            .Select(c => new CategoryCount { Category = c.Name, Count = c.Books.Count })
            .ToListAsync(cancellationToken);
        var uncategorized = await db.Books.CountAsync(b => b.CategoryId == null, cancellationToken);
        if (uncategorized > 0)
        {
            byCategory.Add(new CategoryCount { Category = "Uncategorized", Count = uncategorized });
        }

        var recent = await db.Books.AsNoTracking()
            .Include(b => b.Category)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var mostRead = await db.Loans.AsNoTracking()
            .GroupBy(l => l.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(cancellationToken);

        var mostReadTitles = await db.Books.AsNoTracking()
            .Where(b => mostRead.Select(m => m.BookId).Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Title, cancellationToken);

        return new LibraryStats
        {
            TotalBooks = totalBooks,
            PublishedBooks = published,
            DraftBooks = draft,
            TotalUsers = totalUsers,
            ActiveLoans = activeLoans,
            OverdueLoans = overdueLoans,
            TotalCategories = totalCategories,
            TotalStorageBytes = totalStorage,
            BooksByCategory = byCategory.OrderByDescending(c => c.Count).ToList(),
            RecentBooks = recent.Select(b => b.ToSummary()).ToList(),
            MostRead = mostRead.Select(m => new PopularBook
            {
                BookId = m.BookId,
                Title = mostReadTitles.TryGetValue(m.BookId, out var t) ? t : "(unknown)",
                LoanCount = m.Count
            }).ToList()
        };
    }
}
