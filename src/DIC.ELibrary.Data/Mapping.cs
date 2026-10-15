using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Data;

internal static class Mapping
{
    public static BookSummary ToSummary(this Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Subtitle = b.Subtitle,
        Author = b.Author,
        CategoryName = b.Category?.Name,
        PublicationYear = b.PublicationYear,
        PageCount = b.PageCount,
        CoverPath = b.CoverPath,
        Status = b.Status,
        OcrStatus = b.OcrStatus,
        CreatedAt = b.CreatedAt
    };

    public static BookDetail ToDetail(this Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Subtitle = b.Subtitle,
        Author = b.Author,
        CategoryName = b.Category?.Name,
        CategoryId = b.CategoryId,
        PublicationYear = b.PublicationYear,
        PageCount = b.PageCount,
        CoverPath = b.CoverPath,
        Status = b.Status,
        OcrStatus = b.OcrStatus,
        CreatedAt = b.CreatedAt,
        Publisher = b.Publisher,
        Isbn = b.Isbn,
        Language = b.Language,
        Description = b.Description,
        FileSizeBytes = b.FileSizeBytes,
        IsScanned = b.IsScanned,
        FilePath = b.FilePath,
        Tags = b.Tags.Select(t => t.Tag.Name).OrderBy(n => n).ToList()
    };

    public static UserSummary ToSummary(this User u, int activeLoans = 0) => new()
    {
        Id = u.Id,
        Username = u.Username,
        DisplayName = u.DisplayName,
        Email = u.Email,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        LastLoginAt = u.LastLoginAt,
        ActiveLoans = activeLoans
    };
}
