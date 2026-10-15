using DIC.ELibrary.Core.Domain;

namespace DIC.ELibrary.Core.Models;

public class UserSummary
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastLoginAt { get; init; }
    public int ActiveLoans { get; init; }
}

public class UserEditModel
{
    public Guid? Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;
    public string? Password { get; set; }
}

/// <summary>Outcome of a bulk user import from CSV.</summary>
public class UserImportResult
{
    /// <summary>Total number of data rows read from the file.</summary>
    public int Total { get; init; }

    /// <summary>Number of users successfully created.</summary>
    public int Created { get; init; }

    /// <summary>Number of rows skipped because of validation or duplicate errors.</summary>
    public int Failed { get; init; }

    /// <summary>Human-readable description of each skipped row.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public class SessionInfo
{
    public bool IsAuthenticated { get; init; }
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
}

public class LoanSummary
{
    public Guid Id { get; init; }
    public Guid BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public DateTimeOffset BorrowedAt { get; init; }
    public DateTimeOffset DueAt { get; init; }
    public DateTimeOffset? ReturnedAt { get; init; }
    public LoanStatus Status { get; init; }
    public bool IsOverdue => Status == LoanStatus.Active && DateTimeOffset.UtcNow > DueAt;
}

public class BookmarkSummary
{
    public Guid Id { get; init; }
    public Guid BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public int Page { get; init; }
    public string? Label { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
