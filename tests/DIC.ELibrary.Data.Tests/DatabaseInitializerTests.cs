using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task Initialize_SeedsAdministrator()
    {
        await using var host = await TestLibraryHost.CreateAsync();

        await using var db = await host.GetService<IDbContextFactory<LibraryDbContext>>().CreateDbContextAsync();
        var admin = await db.Users.SingleAsync();

        Assert.Equal("admin", admin.Username);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.True(admin.IsActive);
        Assert.NotEmpty(admin.PasswordHash);
    }

    [Fact]
    public async Task Initialize_SeedsCategoriesAndSettings()
    {
        await using var host = await TestLibraryHost.CreateAsync();

        await using var db = await host.GetService<IDbContextFactory<LibraryDbContext>>().CreateDbContextAsync();

        Assert.Equal(7, await db.Categories.CountAsync());
        Assert.True(await db.AppSettings.AnyAsync(s => s.Key == "LibraryName"));
        Assert.True(await db.AppSettings.AnyAsync(s => s.Key == "Theme"));
    }

    [Fact]
    public async Task Initialize_CreatesFullTextSearchTable()
    {
        await using var host = await TestLibraryHost.CreateAsync();

        await using var db = await host.GetService<IDbContextFactory<LibraryDbContext>>().CreateDbContextAsync();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'BookSearch'";

        var name = (string?)await command.ExecuteScalarAsync();

        Assert.Equal("BookSearch", name);
    }

    [Fact]
    public async Task Initialize_IsIdempotent()
    {
        await using var host = await TestLibraryHost.CreateAsync();

        var initializer = host.GetService<DatabaseInitializer>();
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var db = await host.GetService<IDbContextFactory<LibraryDbContext>>().CreateDbContextAsync();

        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(7, await db.Categories.CountAsync());
    }
}
