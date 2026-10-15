using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_WithSeededAdmin_Succeeds()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var auth = host.GetService<IAuthService>();

        var result = await auth.LoginAsync("admin", "Admin@123");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.IsAuthenticated);
        Assert.Equal(UserRole.Administrator, result.Value.Role);
        Assert.Equal("admin", result.Value.Username);
        Assert.True(host.GetService<ICurrentUser>().IsAuthenticated);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var auth = host.GetService<IAuthService>();

        var result = await auth.LoginAsync("admin", "wrong-password");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.False(host.GetService<ICurrentUser>().IsAuthenticated);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var auth = host.GetService<IAuthService>();

        var result = await auth.LoginAsync("ghost", "whatever");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Login_EmptyCredentials_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var auth = host.GetService<IAuthService>();

        Assert.False((await auth.LoginAsync("", "")).Succeeded);
    }

    [Fact]
    public async Task Login_DisabledAccount_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var auth = host.GetService<IAuthService>();

        var created = await users.CreateAsync(
            new UserEditModel { Username = "stu", DisplayName = "Student", Role = UserRole.Student },
            "Student@123");
        Assert.True(created.Succeeded);
        await users.SetActiveAsync(created.Value!.Id, false);

        var result = await auth.LoginAsync("stu", "Student@123");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task EnsureSeedAdmin_ReactivatesDisabledAdministrator()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var factory = host.GetService<IDbContextFactory<LibraryDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            admin.IsActive = false;
            await db.SaveChangesAsync();
        }

        var auth = host.GetService<IAuthService>();
        Assert.False((await auth.LoginAsync("admin", "Admin@123")).Succeeded);

        await auth.EnsureSeedAdminAsync();

        Assert.True((await auth.LoginAsync("admin", "Admin@123")).Succeeded);
    }

    [Fact]
    public async Task ChangePassword_UpdatesCredentials()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var auth = host.GetService<IAuthService>();
        var login = await auth.LoginAsync("admin", "Admin@123");
        Assert.True(login.Succeeded);

        var changed = await auth.ChangePasswordAsync(login.Value!.UserId, "Admin@123", "NewPass@456");

        Assert.True(changed.Succeeded);
        Assert.False((await auth.LoginAsync("admin", "Admin@123")).Succeeded);
        Assert.True((await auth.LoginAsync("admin", "NewPass@456")).Succeeded);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var auth = host.GetService<IAuthService>();
        var login = await auth.LoginAsync("admin", "Admin@123");

        var changed = await auth.ChangePasswordAsync(login.Value!.UserId, "not-my-password", "NewPass@456");

        Assert.False(changed.Succeeded);
    }
}
