using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Services;

public sealed class LoanService : ILoanService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IClock _clock;
    private readonly LibraryOptions _library;
    private readonly IAuditService _audit;

    public LoanService(IDbContextFactory<LibraryDbContext> factory, IClock clock, IOptions<LibraryOptions> library, IAuditService audit)
    {
        _factory = factory;
        _clock = clock;
        _library = library.Value;
        _audit = audit;
    }

    public async Task<OperationResult<LoanSummary>> BorrowAsync(Guid userId, Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);
        if (book is null)
        {
            return OperationResult<LoanSummary>.Fail("Book not found.");
        }
        if (book.Status != BookStatus.Published)
        {
            return OperationResult<LoanSummary>.Fail("This book is not available for borrowing.");
        }

        var already = await db.Loans.AnyAsync(l => l.UserId == userId && l.BookId == bookId && l.Status != LoanStatus.Returned, cancellationToken);
        if (already)
        {
            return OperationResult<LoanSummary>.Fail("You already have an active loan for this book.");
        }

        if (_library.MaxConcurrentLoans > 0)
        {
            var active = await db.Loans.CountAsync(l => l.UserId == userId && l.Status != LoanStatus.Returned, cancellationToken);
            if (active >= _library.MaxConcurrentLoans)
            {
                return OperationResult<LoanSummary>.Fail($"You have reached the limit of {_library.MaxConcurrentLoans} active loans.");
            }
        }

        var loan = new Loan
        {
            UserId = userId,
            BookId = bookId,
            BorrowedAt = _clock.UtcNow,
            DueAt = _clock.UtcNow.AddDays(_library.DefaultLoanDays),
            Status = LoanStatus.Active
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.LoanCreated, nameof(Loan), loan.Id.ToString(), book.Title, cancellationToken);

        return OperationResult<LoanSummary>.Ok(new LoanSummary
        {
            Id = loan.Id,
            BookId = bookId,
            BookTitle = book.Title,
            UserId = userId,
            BorrowedAt = loan.BorrowedAt,
            DueAt = loan.DueAt,
            Status = loan.Status
        });
    }

    public async Task<OperationResult> ReturnAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return OperationResult.Fail("Loan not found.");
        }
        loan.ReturnedAt = _clock.UtcNow;
        loan.Status = LoanStatus.Returned;
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.LoanReturned, nameof(Loan), loanId.ToString(), null, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<IReadOnlyList<LoanSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var loans = await db.Loans.AsNoTracking()
            .Include(l => l.Book)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.BorrowedAt)
            .ToListAsync(cancellationToken);
        return loans.Select(ToSummary).ToList();
    }

    public async Task<PagedResult<LoanSummary>> QueryAsync(LoanStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var q = db.Loans.AsNoTracking().Include(l => l.Book).Include(l => l.User).AsQueryable();
        if (status is not null)
        {
            q = q.Where(l => l.Status == status);
        }

        var total = await q.CountAsync(cancellationToken);
        var loans = await q.OrderByDescending(l => l.BorrowedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LoanSummary>
        {
            Items = loans.Select(ToSummary).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> RefreshOverdueAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var overdue = await db.Loans
            .Where(l => l.Status == LoanStatus.Active && l.DueAt < _clock.UtcNow)
            .ToListAsync(cancellationToken);
        foreach (var loan in overdue)
        {
            loan.Status = LoanStatus.Overdue;
        }
        if (overdue.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return overdue.Count;
    }

    private static LoanSummary ToSummary(Loan l) => new()
    {
        Id = l.Id,
        BookId = l.BookId,
        BookTitle = l.Book?.Title ?? "(unknown)",
        UserId = l.UserId,
        Username = l.User?.Username ?? string.Empty,
        BorrowedAt = l.BorrowedAt,
        DueAt = l.DueAt,
        ReturnedAt = l.ReturnedAt,
        Status = l.Status
    };
}
