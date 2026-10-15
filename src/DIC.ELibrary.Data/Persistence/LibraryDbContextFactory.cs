using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DIC.ELibrary.Data.Persistence;

/// <summary>Design-time factory so <c>dotnet ef</c> can build the context without a running host.</summary>
public class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DIC",
            "ELibrary");
        Directory.CreateDirectory(dataDir);

        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite($"Data Source={Path.Combine(dataDir, "library.db")}")
            .Options;

        return new LibraryDbContext(options);
    }
}
