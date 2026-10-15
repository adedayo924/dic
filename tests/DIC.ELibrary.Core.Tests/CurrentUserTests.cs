using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Services;

namespace DIC.ELibrary.Core.Tests;

public class CurrentUserTests
{
    [Fact]
    public void NewInstance_IsNotAuthenticated()
    {
        var user = new CurrentUser();

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.Session);
        Assert.Null(user.UserId);
        Assert.Null(user.Username);
    }

    [Fact]
    public void SetSession_PopulatesIdentity()
    {
        var user = new CurrentUser();
        var id = Guid.NewGuid();

        user.SetSession(new SessionInfo
        {
            IsAuthenticated = true,
            UserId = id,
            Username = "alice",
            DisplayName = "Alice",
            Role = UserRole.Librarian
        });

        Assert.True(user.IsAuthenticated);
        Assert.Equal(id, user.UserId);
        Assert.Equal("alice", user.Username);
        Assert.True(user.IsInRole(UserRole.Librarian));
        Assert.False(user.IsInRole(UserRole.Administrator));
    }

    [Fact]
    public void SetSession_Null_ClearsIdentity()
    {
        var user = new CurrentUser();
        user.SetSession(new SessionInfo { IsAuthenticated = true, Username = "bob" });

        user.SetSession(null);

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.Username);
    }

    [Fact]
    public void Teacher_IsNotStaff()
    {
        var user = new CurrentUser();
        user.SetSession(new SessionInfo { IsAuthenticated = true, Username = "t", Role = UserRole.Teacher });

        Assert.True(user.IsInRole(UserRole.Teacher));
        Assert.False(user.IsInRole(UserRole.Librarian));
        Assert.False(user.IsInRole(UserRole.Administrator));
    }
}
