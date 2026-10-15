using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Persistence;

/// <summary>Applies migrations, provisions the SQLite FTS5 index and seeds default data.</summary>
public class DatabaseInitializer
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SecurityOptions _security;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<LibraryDbContext> factory,
        IPasswordHasher passwordHasher,
        IOptions<SecurityOptions> security,
        ILogger<DatabaseInitializer> logger)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
        _security = security.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        await db.Database.MigrateAsync(cancellationToken);
        await EnsureFullTextSearchAsync(db, cancellationToken);
        await SeedAsync(db, cancellationToken);
    }

    private static async Task EnsureFullTextSearchAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE VIRTUAL TABLE IF NOT EXISTS BookSearch USING fts5(
                BookId UNINDEXED,
                Title,
                Author,
                Description,
                Body,
                tokenize = 'unicode61'
            );
            """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task SeedAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var admin = new User
            {
                Username = _security.DefaultAdminUsername,
                DisplayName = "Administrator",
                Role = UserRole.Administrator,
                IsActive = true,
                PasswordHash = _passwordHasher.Hash(_security.DefaultAdminPassword)
            };
            db.Users.Add(admin);
            _logger.LogInformation("Seeded default administrator account '{Username}'.", admin.Username);
        }

        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            string[] defaults = { "General", "Science", "Mathematics", "Literature", "Technology", "History", "Reference" };
            for (var i = 0; i < defaults.Length; i++)
            {
                db.Categories.Add(new Category { Name = defaults[i], SortOrder = i });
            }
        }

        if (!await db.AppSettings.AnyAsync(cancellationToken))
        {
            db.AppSettings.Add(new AppSetting { Key = "LibraryName", Value = "DIC E-Library" });
            db.AppSettings.Add(new AppSetting { Key = "Theme", Value = "Light" });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
