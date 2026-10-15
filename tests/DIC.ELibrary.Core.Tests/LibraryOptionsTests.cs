using DIC.ELibrary.Core.Options;

namespace DIC.ELibrary.Core.Tests;

public class LibraryOptionsTests
{
    [Fact]
    public void ResolvedDataDirectory_FallsBackToLocalAppData()
    {
        var options = new LibraryOptions();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DIC",
            "ELibrary");

        Assert.Equal(expected, options.ResolvedDataDirectory);
    }

    [Fact]
    public void ResolvedDataDirectory_UsesConfiguredValue()
    {
        var options = new LibraryOptions { DataDirectory = @"C:\data\library" };

        Assert.Equal(@"C:\data\library", options.ResolvedDataDirectory);
        Assert.Equal(Path.Combine(@"C:\data\library", "library.db"), options.DatabasePath);
        Assert.Equal(Path.Combine(@"C:\data\library", "books"), options.BooksPath);
    }

    [Fact]
    public void ToAbsolute_ResolvesRelativeAgainstDataDirectory()
    {
        var options = new LibraryOptions { DataDirectory = @"C:\data\library" };

        var absolute = options.ToAbsolute(Path.Combine("books", "abc", "book.pdf"));

        Assert.Equal(Path.Combine(@"C:\data\library", "books", "abc", "book.pdf"), absolute);
    }

    [Fact]
    public void ToAbsolute_PreservesRootedPaths_AndEmpty()
    {
        var options = new LibraryOptions { DataDirectory = @"C:\data\library" };

        Assert.Equal(@"D:\other\file.pdf", options.ToAbsolute(@"D:\other\file.pdf"));
        Assert.Equal(string.Empty, options.ToAbsolute(null));
        Assert.Equal(string.Empty, options.ToAbsolute("   "));
    }

    [Fact]
    public void ToRelative_StripsDataDirectory()
    {
        var options = new LibraryOptions { DataDirectory = @"C:\data\library" };
        var absolute = Path.Combine(@"C:\data\library", "books", "abc", "book.pdf");

        var relative = options.ToRelative(absolute);

        Assert.Equal(Path.Combine("books", "abc", "book.pdf"), relative);
    }

    [Fact]
    public void BookDirectory_IsUnderBooksPath()
    {
        var options = new LibraryOptions { DataDirectory = @"C:\data\library" };
        var id = Guid.NewGuid();

        var dir = options.BookDirectory(id);

        Assert.Equal(Path.Combine(options.BooksPath, id.ToString("N")), dir);
    }
}
