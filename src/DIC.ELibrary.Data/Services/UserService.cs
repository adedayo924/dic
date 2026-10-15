using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class UserService : IUserService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _audit;

    public UserService(IDbContextFactory<LibraryDbContext> factory, IPasswordHasher passwordHasher, IAuditService audit)
    {
        _factory = factory;
        _passwordHasher = passwordHasher;
        _audit = audit;
    }

    public async Task<PagedResult<UserSummary>> QueryAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.Username.Contains(s) || u.DisplayName.Contains(s) || (u.Email != null && u.Email.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = users.Select(u => u.Id).ToList();
        var loanCounts = await db.Loans.AsNoTracking()
            .Where(l => ids.Contains(l.UserId) && l.Status == LoanStatus.Active)
            .GroupBy(l => l.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var items = users.Select(u => u.ToSummary(loanCounts.TryGetValue(u.Id, out var c) ? c : 0)).ToList();
        return new PagedResult<UserSummary> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<UserSummary?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var activeLoans = await db.Loans.CountAsync(l => l.UserId == id && l.Status == LoanStatus.Active, cancellationToken);
        return user.ToSummary(activeLoans);
    }

    public async Task<OperationResult<UserSummary>> CreateAsync(UserEditModel model, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Username))
        {
            return OperationResult<UserSummary>.Fail("Username is required.");
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return OperationResult<UserSummary>.Fail("Password must be at least 6 characters.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (await db.Users.AnyAsync(u => u.Username == model.Username, cancellationToken))
        {
            return OperationResult<UserSummary>.Fail("A user with that username already exists.");
        }

        var user = new User
        {
            Username = model.Username.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.Username.Trim() : model.DisplayName.Trim(),
            Email = model.Email,
            Role = model.Role,
            IsActive = model.IsActive,
            PasswordHash = _passwordHasher.Hash(password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.UserCreated, nameof(User), user.Id.ToString(), user.Username, cancellationToken);
        return OperationResult<UserSummary>.Ok(user.ToSummary());
    }

    public async Task<OperationResult> UpdateAsync(UserEditModel model, CancellationToken cancellationToken = default)
    {
        if (model.Id is null)
        {
            return OperationResult.Fail("User id is required.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == model.Id, cancellationToken);
        if (user is null)
        {
            return OperationResult.Fail("User not found.");
        }

        if (!string.Equals(user.Username, model.Username, StringComparison.OrdinalIgnoreCase) &&
            await db.Users.AnyAsync(u => u.Username == model.Username && u.Id != user.Id, cancellationToken))
        {
            return OperationResult.Fail("A user with that username already exists.");
        }

        user.Username = model.Username.Trim();
        user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? model.Username.Trim() : model.DisplayName.Trim();
        user.Email = model.Email;
        user.Role = model.Role;
        user.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            user.PasswordHash = _passwordHasher.Hash(model.Password);
        }

        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.UserUpdated, nameof(User), user.Id.ToString(), user.Username, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return OperationResult.Fail("User not found.");
        }
        if (user.Role == UserRole.Administrator &&
            await db.Users.CountAsync(u => u.Role == UserRole.Administrator, cancellationToken) <= 1)
        {
            return OperationResult.Fail("Cannot delete the last administrator.");
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.UserDeleted, nameof(User), id.ToString(), user.Username, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return OperationResult.Fail("User not found.");
        }

        if (!isActive && user.Role == UserRole.Administrator &&
            !await db.Users.AnyAsync(u => u.Role == UserRole.Administrator && u.IsActive && u.Id != user.Id, cancellationToken))
        {
            return OperationResult.Fail("Cannot disable the last active administrator.");
        }

        user.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.UserUpdated, nameof(User), id.ToString(), isActive ? "Enabled" : "Disabled", cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return OperationResult.Fail("Password must be at least 6 characters.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return OperationResult.Fail("User not found.");
        }
        user.PasswordHash = _passwordHasher.Hash(newPassword);
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.PasswordChanged, nameof(User), id.ToString(), "Password reset by administrator.", cancellationToken);
        return OperationResult.Ok();
    }

    public string GetImportTemplate() => UserCsvTemplate.Content;

    public async Task<OperationResult<UserImportResult>> ImportAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        if (csvStream is null)
        {
            return OperationResult<UserImportResult>.Fail("No file was provided.");
        }

        List<UserCsvRow> rows;
        try
        {
            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
            });
            rows = csv.GetRecords<UserCsvRow>().ToList();
        }
        catch (Exception ex)
        {
            return OperationResult<UserImportResult>.Fail($"Could not read the CSV file: {ex.Message}");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Users.AsNoTracking().Select(u => u.Username).ToListAsync(cancellationToken);
        var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var created = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2; // Header occupies line 1.
            var username = row.Username?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                errors.Add($"Line {line}: username is required.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.Password) || row.Password.Length < 6)
            {
                errors.Add($"Line {line} ('{username}'): password must be at least 6 characters.");
                continue;
            }
            if (!TryParseRole(row.Role, out var role))
            {
                errors.Add($"Line {line} ('{username}'): '{row.Role}' is not a valid role. " +
                           "Use Student, Teacher, Librarian or Administrator.");
                continue;
            }
            if (!seen.Add(username))
            {
                errors.Add($"Line {line} ('{username}'): a user with that username already exists.");
                continue;
            }

            db.Users.Add(new User
            {
                Username = username,
                DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? username : row.DisplayName!.Trim(),
                Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email!.Trim(),
                Role = role,
                IsActive = row.IsActive ?? true,
                PasswordHash = _passwordHasher.Hash(row.Password)
            });
            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(AuditAction.UserCreated, nameof(User), null, $"Bulk import created {created} user(s).", cancellationToken);
        }

        var summary = new UserImportResult
        {
            Total = rows.Count,
            Created = created,
            Failed = errors.Count,
            Errors = errors
        };
        return OperationResult<UserImportResult>.Ok(summary);
    }

    private static bool TryParseRole(string? value, out UserRole role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = UserRole.Student;
            return true;
        }
        return Enum.TryParse(value.Trim(), ignoreCase: true, out role) && Enum.IsDefined(role);
    }
}

/// <summary>A single row read from the bulk user import CSV file.</summary>
public class UserCsvRow
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Password { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>Defines the header and sample content of the bulk user import CSV file.</summary>
public static class UserCsvTemplate
{
    public const string Content =
        "Username,DisplayName,Email,Role,Password,IsActive\r\n" +
        "jdoe,Jane Doe,jane@example.com,Student,ChangeMe123,true\r\n" +
        "asmith,Alan Smith,alan@example.com,Teacher,ChangeMe123,true\r\n" +
        "mcurie,Marie Curie,marie@example.com,Librarian,ChangeMe123,true\r\n";
}
