namespace DIC.ELibrary.Core.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public ICollection<ReadingProgress> Progress { get; set; } = new List<ReadingProgress>();
}

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Book> Books { get; set; } = new List<Book>();
}

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public ICollection<BookTag> Books { get; set; } = new List<BookTag>();
}

public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? Isbn { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }
    public int PageCount { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Draft;
    public OcrStatus OcrStatus { get; set; } = OcrStatus.NotApplicable;
    public string? CoverPath { get; set; }
    public string? FilePath { get; set; }
    public string? TextPath { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsScanned { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public string? SourcePackageId { get; set; }

    public ICollection<BookTag> Tags { get; set; } = new List<BookTag>();
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public ICollection<ReadingProgress> Progress { get; set; } = new List<ReadingProgress>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}

public class BookTag
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class Loan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public DateTimeOffset BorrowedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
}

public class Favorite
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int Page { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ReadingProgress
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int LastPage { get; set; }
    public double Percent { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public AuditAction Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class PackageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PackageId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public Guid? BookId { get; set; }
    public string? Version { get; set; }
    public long SizeBytes { get; set; }
    public string? Checksum { get; set; }
    public bool IsInstalled { get; set; }
    public DateTimeOffset BuiltAt { get; set; } = DateTimeOffset.UtcNow;
}
