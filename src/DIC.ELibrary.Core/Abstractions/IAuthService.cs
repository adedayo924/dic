using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Abstractions;

/// <summary>Holds the currently signed-in identity for the running app.</summary>
public interface ICurrentUser
{
    SessionInfo? Session { get; }
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Username { get; }
    bool IsInRole(Domain.UserRole role);
    void SetSession(SessionInfo? session);
}

public interface IAuthService
{
    Task<OperationResult<SessionInfo>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task EnsureSeedAdminAsync(CancellationToken cancellationToken = default);
}
