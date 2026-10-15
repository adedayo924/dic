using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Data.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task Create_ValidUser_Succeeds()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();

        var result = await users.CreateAsync(
            new UserEditModel { Username = "alice", DisplayName = "Alice", Email = "alice@example.com", Role = UserRole.Student },
            "Alice@123");

        Assert.True(result.Succeeded);
        Assert.Equal("alice", result.Value!.Username);
        Assert.Equal(UserRole.Student, result.Value.Role);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Create_ShortPassword_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();

        var result = await users.CreateAsync(
            new UserEditModel { Username = "bob", Role = UserRole.Student },
            "123");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_DuplicateUsername_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var model = new UserEditModel { Username = "carol", Role = UserRole.Student };

        Assert.True((await users.CreateAsync(model, "Carol@123")).Succeeded);
        var duplicate = await users.CreateAsync(model, "Carol@123");

        Assert.False(duplicate.Succeeded);
    }

    [Fact]
    public async Task Delete_LastAdministrator_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var admin = await users.QueryAsync(null, 1, 10);
        var adminId = admin.Items.Single(u => u.Role == UserRole.Administrator).Id;

        var result = await users.DeleteAsync(adminId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_WhenAnotherAdministratorExists_Succeeds()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var adminId = (await users.QueryAsync(null, 1, 10)).Items.Single(u => u.Role == UserRole.Administrator).Id;

        var second = await users.CreateAsync(
            new UserEditModel { Username = "admin2", DisplayName = "Admin Two", Role = UserRole.Administrator },
            "Admin2@123");
        Assert.True(second.Succeeded);

        var result = await users.DeleteAsync(adminId);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SetActive_LastAdministrator_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var adminId = (await users.QueryAsync(null, 1, 10)).Items.Single(u => u.Role == UserRole.Administrator).Id;

        var result = await users.SetActiveAsync(adminId, false);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SetActive_WhenAnotherActiveAdministratorExists_Succeeds()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var adminId = (await users.QueryAsync(null, 1, 10)).Items.Single(u => u.Role == UserRole.Administrator).Id;
        await users.CreateAsync(
            new UserEditModel { Username = "admin2", DisplayName = "Admin Two", Role = UserRole.Administrator },
            "Admin2@123");

        var result = await users.SetActiveAsync(adminId, false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ResetPassword_LetsUserAuthenticate()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var auth = host.GetService<IAuthService>();

        var created = await users.CreateAsync(
            new UserEditModel { Username = "dave", Role = UserRole.Student },
            "Dave@123");

        Assert.True((await users.ResetPasswordAsync(created.Value!.Id, "Reset@456")).Succeeded);
        Assert.False((await auth.LoginAsync("dave", "Dave@123")).Succeeded);
        Assert.True((await auth.LoginAsync("dave", "Reset@456")).Succeeded);
    }

    [Fact]
    public async Task Query_FiltersBySearchTerm()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        await users.CreateAsync(new UserEditModel { Username = "findme", DisplayName = "Find Me", Role = UserRole.Student }, "Find@123");
        await users.CreateAsync(new UserEditModel { Username = "other", DisplayName = "Other", Role = UserRole.Student }, "Other@123");

        var page = await users.QueryAsync("find", 1, 20);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal("findme", page.Items.Single().Username);
    }

    [Fact]
    public async Task Import_ValidCsv_CreatesUsersWithRoles()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();

        var csv = "Username,DisplayName,Email,Role,Password,IsActive\n" +
                  "newstudent,New Student,ns@example.com,Student,Student@123,true\n" +
                  "newteacher,New Teacher,,Teacher,Teacher@123,true\n";
        var result = await users.ImportAsync(Csv(csv));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Created);
        Assert.Equal(0, result.Value.Failed);

        var page = await users.QueryAsync("new", 1, 20);
        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, u => u.Username == "newteacher" && u.Role == UserRole.Teacher);
    }

    [Fact]
    public async Task Import_ReportsInvalidRows()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();

        var csv = "Username,DisplayName,Email,Role,Password,IsActive\n" +
                  ",No Username,,Student,Student@123,true\n" +
                  "shortpw,Short Password,,Student,123,true\n" +
                  "badrole,Bad Role,,Wizard,Student@123,true\n" +
                  "good,Good User,,Student,Good@123,true\n";
        var result = await users.ImportAsync(Csv(csv));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Created);
        Assert.Equal(3, result.Value.Failed);
        Assert.Contains(result.Value.Errors, e => e.Contains("username is required"));
        Assert.Contains(result.Value.Errors, e => e.Contains("at least 6"));
        Assert.Contains(result.Value.Errors, e => e.Contains("not a valid role"));
    }

    [Fact]
    public async Task Import_SkipsExistingAndDuplicateUsernames()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        await users.CreateAsync(new UserEditModel { Username = "existing", Role = UserRole.Student }, "Existing@123");

        var csv = "Username,Role,Password\n" +
                  "existing,Student,Other@123\n" +
                  "fresh,Student,Fresh@123\n" +
                  "fresh,Student,Fresh@123\n";
        var result = await users.ImportAsync(Csv(csv));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Created);
        Assert.Equal(2, result.Value.Failed);
        Assert.All(result.Value.Errors, e => Assert.Contains("already exists", e));
    }

    [Fact]
    public async Task Import_DefaultTemplate_IsImportable()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();

        var template = users.GetImportTemplate();
        Assert.Contains("Username", template);

        var result = await users.ImportAsync(Csv(template));

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.Created);
        Assert.Equal(0, result.Value.Failed);
    }

    private static Stream Csv(string content) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
}
