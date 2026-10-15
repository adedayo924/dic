using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;

    public AppSettingsService(IDbContextFactory<LibraryDbContext> factory) => _factory = factory;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.AppSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
    }
}
