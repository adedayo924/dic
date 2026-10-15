namespace DIC.ELibrary.Core.Options;

/// <summary>
/// Filesystem locations and behavioral settings for the library.
/// </summary>
public class LibraryOptions
{
    public const string SectionName = "Library";

    /// <summary>Root data directory. Defaults to %LOCALAPPDATA%\DIC\ELibrary.</summary>
    public string DataDirectory { get; set; } = string.Empty;

    public string LibraryName { get; set; } = "DIC E-Library";

    /// <summary>Default loan length in days.</summary>
    public int DefaultLoanDays { get; set; } = 14;

    /// <summary>Maximum number of concurrent active loans per student. 0 = unlimited.</summary>
    public int MaxConcurrentLoans { get; set; } = 0;

    public string DatabaseFileName { get; set; } = "library.db";
    public string BooksFolderName { get; set; } = "books";
    public string PackagesFolderName { get; set; } = "packages";
    public string LogsFolderName { get; set; } = "logs";
    public string CoversFolderName { get; set; } = "covers";
    public string BackupsFolderName { get; set; } = "backups";

    public string DatabasePath => Path.Combine(ResolvedDataDirectory, DatabaseFileName);
    public string BooksPath => Path.Combine(ResolvedDataDirectory, BooksFolderName);
    public string PackagesPath => Path.Combine(ResolvedDataDirectory, PackagesFolderName);
    public string LogsPath => Path.Combine(ResolvedDataDirectory, LogsFolderName);
    public string CoversPath => Path.Combine(ResolvedDataDirectory, CoversFolderName);
    public string BackupsPath => Path.Combine(ResolvedDataDirectory, BackupsFolderName);

    public string ResolvedDataDirectory =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DIC",
                "ELibrary")
            : DataDirectory;

    /// <summary>Resolves a stored relative path (as kept in the database) to an absolute path.</summary>
    public string ToAbsolute(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }
        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(ResolvedDataDirectory, relativePath);
    }

    /// <summary>Converts an absolute path under the data directory to a portable relative path.</summary>
    public string ToRelative(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }
        var root = Path.GetFullPath(ResolvedDataDirectory);
        var full = Path.GetFullPath(absolutePath);
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(root, full);
        }
        return absolutePath;
    }

    public string BookDirectory(Guid bookId) => Path.Combine(BooksPath, bookId.ToString("N"));
}
