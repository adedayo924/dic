using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Services;

public sealed class CurrentUser : ICurrentUser
{
    public SessionInfo? Session { get; private set; }

    public bool IsAuthenticated => Session?.IsAuthenticated ?? false;
    public Guid? UserId => Session?.UserId;
    public string? Username => Session?.Username;

    public bool IsInRole(Domain.UserRole role) => Session is not null && Session.Role == role;

    public void SetSession(SessionInfo? session) => Session = session;
}
