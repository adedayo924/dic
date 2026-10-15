using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class AuditService : IAuditService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AuditService(IDbContextFactory<LibraryDbContext> factory, ICurrentUser currentUser, IClock clock)
    {
        _factory = factory;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task LogAsync(AuditAction action, string? entityType = null, string? entityId = null, string? details = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = _clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Timestamp);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AuditLog> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}
