using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Abstractions;

public class PackageManifest
{
    public int FormatVersion { get; set; } = 1;
    public string PackageId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? Isbn { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    public int PageCount { get; set; }
    public string ContentFileName { get; set; } = "content.pdf";
    public string? CoverFileName { get; set; }
    public string? TextFileName { get; set; }
    public bool Encrypted { get; set; }
    public bool ContentEncrypted { get; set; }
    public string? PackageVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long ContentSizeBytes { get; set; }
    public string? ContentSha256 { get; set; }
    public string? OriginLibrary { get; set; }
}

public class PackageInspection
{
    public bool IsValid { get; init; }
    public bool SignatureVerified { get; init; }
    public string? Error { get; init; }
    public PackageManifest? Manifest { get; init; }
    public string PackagePath { get; init; } = string.Empty;
}

public interface IPackageService
{
    /// <summary>Builds an encrypted, signed .dicpkg from a catalog book.</summary>
    Task<OperationResult<string>> BuildAsync(Guid bookId, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>Verifies signature/integrity and reads the manifest without extracting content.</summary>
    Task<OperationResult<PackageInspection>> InspectAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>Installs a package into the local library, returning the resulting book id.</summary>
    Task<OperationResult<Guid>> InstallAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>Exports the whole catalog as a signed/encrypted .dicl bundle.</summary>
    Task<OperationResult<string>> ExportCatalogAsync(string outputPath, CancellationToken cancellationToken = default);
}
