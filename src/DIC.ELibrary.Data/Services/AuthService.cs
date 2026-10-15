using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Services;

public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly IClock _clock;
    private readonly SecurityOptions _options;

    public AuthService(
        IDbContextFactory<LibraryDbContext> factory,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IAuditService audit,
        IClock clock,
        IOptions<SecurityOptions> options)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<OperationResult<SessionInfo>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return OperationResult<SessionInfo>.Fail("Username and password are required.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            await _audit.LogAsync(AuditAction.LoginFailed, nameof(User), username, "Invalid credentials.", cancellationToken);
            return OperationResult<SessionInfo>.Fail("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return OperationResult<SessionInfo>.Fail("This account is disabled. Contact the administrator.");
        }

        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.Hash(password);
        }

        user.LastLoginAt = _clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var session = new SessionInfo
        {
            IsAuthenticated = true,
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
        _currentUser.SetSession(session);

        await _audit.LogAsync(AuditAction.Login, nameof(User), user.Id.ToString(), null, cancellationToken);
        return OperationResult<SessionInfo>.Ok(session);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsAuthenticated)
        {
            await _audit.LogAsync(AuditAction.Logout, nameof(User), _currentUser.UserId?.ToString(), null, cancellationToken);
        }
        _currentUser.SetSession(null);
    }

    public async Task<OperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return OperationResult.Fail("New password must be at least 6 characters.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return OperationResult.Fail("User not found.");
        }

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            return OperationResult.Fail("Current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.PasswordChanged, nameof(User), userId.ToString(), null, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task EnsureSeedAdminAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.Add(new User
            {
                Username = _options.DefaultAdminUsername,
                DisplayName = "Administrator",
                Role = UserRole.Administrator,
                IsActive = true,
                PasswordHash = _passwordHasher.Hash(_options.DefaultAdminPassword)
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Recovery: never leave the library without an active administrator, which would
        // otherwise lock every operator out of the administration tool.
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Administrator && u.IsActive, cancellationToken))
        {
            return;
        }

        var fallback =
            await db.Users.FirstOrDefaultAsync(u => u.Username == _options.DefaultAdminUsername, cancellationToken)
            ?? await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Administrator, cancellationToken);

        if (fallback is not null)
        {
            fallback.IsActive = true;
            fallback.Role = UserRole.Administrator;
        }
        else
        {
            db.Users.Add(new User
            {
                Username = _options.DefaultAdminUsername,
                DisplayName = "Administrator",
                Role = UserRole.Administrator,
                IsActive = true,
                PasswordHash = _passwordHasher.Hash(_options.DefaultAdminPassword)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
