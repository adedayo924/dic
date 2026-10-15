using DIC.ELibrary.Core.Domain;

namespace DIC.ELibrary.Core.Models;

public class BookQuery
{
    public string? SearchText { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TagId { get; set; }
    public BookStatus? Status { get; set; }
    public string? Author { get; set; }
    public string? Language { get; set; }
    public int? Year { get; set; }
    public BookSortField SortBy { get; set; } = BookSortField.Title;
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool FullText { get; set; }
}

public enum BookSortField
{
    Title = 0,
    Author = 1,
    PublicationYear = 2,
    RecentlyAdded = 3,
    Popularity = 4
}

public class BookSummary
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string? Author { get; init; }
    public string? CategoryName { get; init; }
    public int? PublicationYear { get; init; }
    public int PageCount { get; init; }
    public string? CoverPath { get; init; }
    public BookStatus Status { get; init; }
    public OcrStatus OcrStatus { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public class BookDetail : BookSummary
{
    public string? Publisher { get; init; }
    public string? Isbn { get; init; }
    public string? Language { get; init; }
    public string? Description { get; init; }
    public long FileSizeBytes { get; init; }
    public bool IsScanned { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public Guid? CategoryId { get; init; }
    public string? FilePath { get; init; }
}

public class BookEditModel
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? Isbn { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Published;
    public List<string> Tags { get; set; } = new();
}

public class SearchHit
{
    public Guid BookId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Author { get; init; }
    public string? Snippet { get; init; }
    public double Rank { get; init; }
}

public class LibraryStats
{
    public int TotalBooks { get; init; }
    public int PublishedBooks { get; init; }
    public int DraftBooks { get; init; }
    public int TotalUsers { get; init; }
    public int ActiveLoans { get; init; }
    public int OverdueLoans { get; init; }
    public int TotalCategories { get; init; }
    public long TotalStorageBytes { get; init; }
    public IReadOnlyList<CategoryCount> BooksByCategory { get; init; } = Array.Empty<CategoryCount>();
    public IReadOnlyList<BookSummary> RecentBooks { get; init; } = Array.Empty<BookSummary>();
    public IReadOnlyList<PopularBook> MostRead { get; init; } = Array.Empty<PopularBook>();
}

public class CategoryCount
{
    public string Category { get; init; } = "Uncategorized";
    public int Count { get; init; }
}

public class PopularBook
{
    public Guid BookId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int LoanCount { get; init; }
}
