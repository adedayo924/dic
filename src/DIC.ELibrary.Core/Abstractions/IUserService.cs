using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Abstractions;

public interface IUserService
{
    Task<PagedResult<UserSummary>> QueryAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<UserSummary?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<UserSummary>> CreateAsync(UserEditModel model, string password, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(UserEditModel model, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Creates users in bulk from a CSV stream, reporting per-row success and failures.</summary>
    Task<OperationResult<UserImportResult>> ImportAsync(Stream csvStream, CancellationToken cancellationToken = default);

    /// <summary>Returns a sample CSV document (header + example rows) for the bulk import file format.</summary>
    string GetImportTemplate();
}
