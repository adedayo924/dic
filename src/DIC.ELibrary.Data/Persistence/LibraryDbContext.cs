using DIC.ELibrary.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DIC.ELibrary.Data.Persistence;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookTag> BookTags => Set<BookTag>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<ReadingProgress> ReadingProgresses => Set<ReadingProgress>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<PackageRecord> Packages => Set<PackageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite cannot ORDER BY DateTimeOffset columns directly; persist them as UTC ticks
        // so range filters and sorting work with the built-in providers.
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero));
        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.UtcTicks : null,
            value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDateTimeOffsetConverter);
                }
            }
        }

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasConversion<int>();
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(1024);
            e.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.ToTable("Tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Book>(e =>
        {
            e.ToTable("Books");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(512);
            e.Property(x => x.Subtitle).HasMaxLength(512);
            e.Property(x => x.Author).HasMaxLength(256);
            e.Property(x => x.Publisher).HasMaxLength(256);
            e.Property(x => x.Isbn).HasMaxLength(32);
            e.Property(x => x.Language).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(8192);
            e.Property(x => x.CoverPath).HasMaxLength(512);
            e.Property(x => x.FilePath).HasMaxLength(512);
            e.Property(x => x.TextPath).HasMaxLength(512);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.OcrStatus).HasConversion<int>();
            e.HasIndex(x => x.Title);
            e.HasIndex(x => x.Author);
            e.HasIndex(x => x.CategoryId);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Books)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BookTag>(e =>
        {
            e.ToTable("BookTags");
            e.HasKey(x => new { x.BookId, x.TagId });
            e.HasOne(x => x.Book).WithMany(b => b.Tags).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag).WithMany(t => t.Books).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Loan>(e =>
        {
            e.ToTable("Loans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.User).WithMany(u => u.Loans).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Book).WithMany(b => b.Loans).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Favorite>(e =>
        {
            e.ToTable("Favorites");
            e.HasKey(x => new { x.UserId, x.BookId });
            e.HasOne(x => x.User).WithMany(u => u.Favorites).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Book).WithMany(b => b.Favorites).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Bookmark>(e =>
        {
            e.ToTable("Bookmarks");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany(u => u.Bookmarks).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Book).WithMany(b => b.Bookmarks).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReadingProgress>(e =>
        {
            e.ToTable("ReadingProgress");
            e.HasKey(x => new { x.UserId, x.BookId });
            e.HasOne(x => x.User).WithMany(u => u.Progress).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Book).WithMany(b => b.Progress).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasConversion<int>();
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.EntityType).HasMaxLength(64);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.Details).HasMaxLength(2048);
            e.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.ToTable("AppSettings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.Value).HasMaxLength(2048);
        });

        modelBuilder.Entity<PackageRecord>(e =>
        {
            e.ToTable("Packages");
            e.HasKey(x => x.Id);
            e.Property(x => x.PackageId).HasMaxLength(64);
            e.Property(x => x.FileName).HasMaxLength(512);
            e.Property(x => x.Version).HasMaxLength(32);
            e.Property(x => x.Checksum).HasMaxLength(128);
            e.HasIndex(x => x.PackageId);
        });
    }
}
