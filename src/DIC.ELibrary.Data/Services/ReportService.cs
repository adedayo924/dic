using System.Globalization;
using CsvHelper;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class ReportService : IReportService
{
    private const string DateFormat = "yyyy-MM-dd HH:mm";
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IClock _clock;

    public ReportService(IDbContextFactory<LibraryDbContext> factory, IClock clock)
    {
        _factory = factory;
        _clock = clock;
    }

    public async Task<string> BuildStatisticsReportCsvAsync(CancellationToken cancellationToken = default)
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
            .Select(c => new { c.Name, Count = c.Books.Count })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var mostRead = await db.Loans.AsNoTracking()
            .GroupBy(l => l.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);
        var mostReadIds = mostRead.Select(m => m.BookId).ToList();
        var titles = await db.Books.AsNoTracking()
            .Where(b => mostReadIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Title, cancellationToken);

        var recent = await db.Books.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .Select(b => new { b.Title, b.CreatedAt })
            .ToListAsync(cancellationToken);

        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        WriteRow(csv, "Section", "Label", "Value");

        WriteRow(csv, "Summary", "Total books", totalBooks.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Published", published.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Draft", draft.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Total users", totalUsers.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Active loans", activeLoans.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Overdue loans", overdueLoans.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Categories", totalCategories.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Storage bytes", totalStorage.ToString(CultureInfo.InvariantCulture));

        foreach (var entry in byCategory)
        {
            WriteRow(csv, "Books by category", entry.Name, entry.Count.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var entry in mostRead)
        {
            var title = titles.TryGetValue(entry.BookId, out var t) ? t : "(unknown)";
            WriteRow(csv, "Most borrowed", title, entry.Count.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var entry in recent)
        {
            WriteRow(csv, "Recent additions", entry.Title, entry.CreatedAt.ToString(DateFormat, CultureInfo.InvariantCulture));
        }

        csv.Flush();
        return writer.ToString();
    }

    public async Task<string> BuildCirculationReportCsvAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var loans = await db.Loans.AsNoTracking()
            .Include(l => l.Book)
            .Include(l => l.User)
            .OrderByDescending(l => l.BorrowedAt)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        WriteRow(csv, "LoanId", "Book", "User", "Borrowed", "Due", "Returned", "Status", "Overdue");

        foreach (var loan in loans)
        {
            var overdue = loan.Status != LoanStatus.Returned && loan.DueAt < now;
            WriteRow(csv,
                loan.Id.ToString(),
                loan.Book?.Title ?? "(unknown)",
                loan.User?.Username ?? string.Empty,
                loan.BorrowedAt.ToString(DateFormat, CultureInfo.InvariantCulture),
                loan.DueAt.ToString(DateFormat, CultureInfo.InvariantCulture),
                loan.ReturnedAt?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty,
                loan.Status.ToString(),
                overdue ? "Yes" : "No");
        }

        csv.Flush();
        return writer.ToString();
    }

    public async Task<string> BuildAuditReportCsvAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var logs = await db.AuditLogs.AsNoTracking()
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        WriteRow(csv, "Id", "Timestamp", "User", "Action", "EntityType", "EntityId", "Details");

        foreach (var log in logs)
        {
            WriteRow(csv,
                log.Id.ToString(CultureInfo.InvariantCulture),
                log.Timestamp.ToString(DateFormat, CultureInfo.InvariantCulture),
                log.Username ?? string.Empty,
                log.Action.ToString(),
                log.EntityType ?? string.Empty,
                log.EntityId ?? string.Empty,
                log.Details ?? string.Empty);
        }

        csv.Flush();
        return writer.ToString();
    }

    private static void WriteRow(CsvWriter csv, params string[] fields)
    {
        foreach (var field in fields)
        {
            csv.WriteField(field);
        }

        csv.NextRecord();
    }
}
