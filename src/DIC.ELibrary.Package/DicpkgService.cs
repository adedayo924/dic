using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DIC.ELibrary.Core;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Package;

/// <summary>
/// Implements the .dicpkg (book package) and .dicl (catalog bundle) formats.
/// Layout of a .dicpkg (a ZIP container):
///   manifest.json  - UTF-8 JSON manifest (the exact bytes that are signed)
///   signature.sig  - RSA-PSS signature over manifest.json
///   content.bin    - AES-256-GCM encrypted PDF payload (or content.pdf when unencrypted)
///   text.bin       - optional encrypted extracted text
///   cover.png      - optional cleartext cover thumbnail
/// </summary>
public sealed class DicpkgService : IPackageService
{
    private const string ManifestEntry = "manifest.json";
    private const string SignatureEntry = "signature.sig";
    private const string ContentEntry = "content.bin";
    private const string PlainContentEntry = "content.pdf";
    private const string TextEntry = "text.bin";
    private const string CoverEntry = "cover.png";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IContentCipher _cipher;
    private readonly IPackageSigner _signer;
    private readonly IKeyManager _keyManager;
    private readonly IPdfService _pdf;
    private readonly IBookService _books;
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IOptions<LibraryOptions> _library;
    private readonly IOptions<PackageOptions> _packages;
    private readonly ILogger<DicpkgService> _logger;

    public DicpkgService(
        IContentCipher cipher,
        IPackageSigner signer,
        IKeyManager keyManager,
        IPdfService pdf,
        IBookService books,
        IDbContextFactory<LibraryDbContext> factory,
        IOptions<LibraryOptions> library,
        IOptions<PackageOptions> packages,
        ILogger<DicpkgService> logger)
    {
        _cipher = cipher;
        _signer = signer;
        _keyManager = keyManager;
        _pdf = pdf;
        _books = books;
        _factory = factory;
        _library = library;
        _packages = packages;
        _logger = logger;
    }

    public async Task<OperationResult<string>> BuildAsync(Guid bookId, string outputPath, CancellationToken cancellationToken = default)
    {
        var book = await _books.GetAsync(bookId, cancellationToken);
        if (book is null)
        {
            return OperationResult<string>.Fail("Book not found.");
        }
        if (string.IsNullOrWhiteSpace(book.FilePath))
        {
            return OperationResult<string>.Fail("This book has no attached PDF to package.");
        }

        var library = _library.Value;
        var packageOptions = _packages.Value;
        var pdfPath = library.ToAbsolute(book.FilePath);
        if (!File.Exists(pdfPath))
        {
            return OperationResult<string>.Fail("The book PDF file is missing on disk.");
        }

        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        var sha = Convert.ToHexString(SHA256.HashData(pdfBytes));

        var manifest = new PackageManifest
        {
            FormatVersion = packageOptions.FormatVersion,
            PackageId = Guid.NewGuid().ToString("N"),
            Title = book.Title,
            Subtitle = book.Subtitle,
            Author = book.Author,
            Publisher = book.Publisher,
            PublicationYear = book.PublicationYear,
            Isbn = book.Isbn,
            Language = book.Language,
            Description = book.Description,
            Category = book.CategoryName,
            Tags = book.Tags,
            PageCount = book.PageCount,
            ContentFileName = packageOptions.Encrypt ? ContentEntry : PlainContentEntry,
            CoverFileName = File.Exists(library.ToAbsolute(book.CoverPath)) ? CoverEntry : null,
            TextFileName = File.Exists(BookTextPath(bookId)) ? TextEntry : null,
            Encrypted = packageOptions.Encrypt,
            ContentEncrypted = packageOptions.Encrypt,
            PackageVersion = AppInfo.Version,
            CreatedAt = DateTimeOffset.UtcNow,
            ContentSizeBytes = pdfBytes.Length,
            ContentSha256 = sha,
            OriginLibrary = library.LibraryName
        };

        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        else
        {
            Directory.CreateDirectory(library.PackagesPath);
            outputPath = Path.Combine(library.PackagesPath, $"{book.Id:N}{packageOptions.Extension}");
        }

        if (packageOptions.Sign && !_signer.HasPrivateKey)
        {
            _keyManager.RegenerateSigningKey();
        }

        await using (var fileStream = File.Create(outputPath))
        using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);

            if (packageOptions.Encrypt)
            {
                await WriteEntryAsync(zip, ContentEntry, _cipher.Encrypt(pdfBytes), cancellationToken);
            }
            else
            {
                await WriteEntryAsync(zip, PlainContentEntry, pdfBytes, cancellationToken);
            }

            var textPath = BookTextPath(bookId);
            if (File.Exists(textPath))
            {
                var textBytes = await File.ReadAllBytesAsync(textPath, cancellationToken);
                await WriteEntryAsync(zip, TextEntry, packageOptions.Encrypt ? _cipher.Encrypt(textBytes) : textBytes, cancellationToken);
            }

            var coverPath = library.ToAbsolute(book.CoverPath);
            if (File.Exists(coverPath))
            {
                await WriteEntryAsync(zip, CoverEntry, await File.ReadAllBytesAsync(coverPath, cancellationToken), cancellationToken);
            }

            await WriteEntryAsync(zip, ManifestEntry, manifestBytes, cancellationToken);

            if (packageOptions.Sign)
            {
                await WriteEntryAsync(zip, SignatureEntry, _signer.Sign(manifestBytes), cancellationToken);
            }
        }

        await RecordPackageAsync(manifest, outputPath, bookId, cancellationToken);
        _logger.LogInformation("Built package {Package} for book {Book}.", outputPath, book.Title);
        return OperationResult<string>.Ok(outputPath);
    }

    public async Task<OperationResult<PackageInspection>> InspectAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(packagePath))
            {
                return OperationResult<PackageInspection>.Fail("Package file not found.");
            }

            using var zip = ZipFile.OpenRead(packagePath);
            var manifestEntry = zip.GetEntry(ManifestEntry);
            if (manifestEntry is null)
            {
                return OperationResult<PackageInspection>.Ok(new PackageInspection
                {
                    IsValid = false,
                    Error = "Manifest is missing.",
                    PackagePath = packagePath
                });
            }

            var manifestBytes = await ReadEntryAsync(manifestEntry, cancellationToken);
            var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestBytes, JsonOptions);

            var signatureVerified = false;
            var signatureEntry = zip.GetEntry(SignatureEntry);
            if (signatureEntry is not null)
            {
                var signature = await ReadEntryAsync(signatureEntry, cancellationToken);
                signatureVerified = _signer.Verify(manifestBytes, signature);
            }

            return OperationResult<PackageInspection>.Ok(new PackageInspection
            {
                IsValid = manifest is not null && signatureVerified,
                SignatureVerified = signatureVerified,
                Manifest = manifest,
                PackagePath = packagePath,
                Error = signatureVerified ? null : "Signature could not be verified."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect package {Package}.", packagePath);
            return OperationResult<PackageInspection>.Ok(new PackageInspection
            {
                IsValid = false,
                Error = ex.Message,
                PackagePath = packagePath
            });
        }
    }

    public async Task<OperationResult<Guid>> InstallAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var inspection = await InspectAsync(packagePath, cancellationToken);
        if (!inspection.Succeeded || inspection.Value is null || !inspection.Value.IsValid)
        {
            var reason = inspection.Value?.Error ?? inspection.Error ?? "Invalid package.";
            return OperationResult<Guid>.Fail(reason);
        }

        var manifest = inspection.Value.Manifest!;
        using var zip = ZipFile.OpenRead(packagePath);

        var contentEntry = zip.GetEntry(ContentEntry) ?? zip.GetEntry(PlainContentEntry);
        if (contentEntry is null)
        {
            return OperationResult<Guid>.Fail("Package content is missing.");
        }

        byte[] pdfBytes = await ReadEntryAsync(contentEntry, cancellationToken);
        if (manifest.ContentEncrypted)
        {
            pdfBytes = _cipher.Decrypt(pdfBytes);
        }

        if (!string.IsNullOrEmpty(manifest.ContentSha256))
        {
            var hash = Convert.ToHexString(SHA256.HashData(pdfBytes));
            if (!string.Equals(hash, manifest.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<Guid>.Fail("Content integrity check failed.");
            }
        }

        var tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempPdf, pdfBytes, cancellationToken);

        try
        {
            var model = new BookEditModel
            {
                Title = manifest.Title,
                Subtitle = manifest.Subtitle,
                Author = manifest.Author,
                Publisher = manifest.Publisher,
                PublicationYear = manifest.PublicationYear,
                Isbn = manifest.Isbn,
                Language = manifest.Language,
                Description = manifest.Description,
                Status = BookStatus.Published,
                Tags = manifest.Tags.ToList()
            };

            var created = await _books.CreateAsync(model, tempPdf, cancellationToken);
            if (!created.Succeeded || created.Value is null)
            {
                return OperationResult<Guid>.Fail(created.Error ?? "Failed to create book.");
            }

            await AttachPackageAssetsAsync(zip, created.Value.Id, manifest, cancellationToken);
            return OperationResult<Guid>.Ok(created.Value.Id);
        }
        finally
        {
            try
            {
                File.Delete(tempPdf);
            }
            catch
            {
                // best effort
            }
        }
    }

    private async Task AttachPackageAssetsAsync(ZipArchive zip, Guid bookId, PackageManifest manifest, CancellationToken cancellationToken)
    {
        var library = _library.Value;
        var dir = library.BookDirectory(bookId);
        Directory.CreateDirectory(dir);

        string? coverRelative = null;
        var coverEntry = zip.GetEntry(CoverEntry);
        if (coverEntry is not null)
        {
            var coverPath = Path.Combine(dir, "cover.png");
            await File.WriteAllBytesAsync(coverPath, await ReadEntryAsync(coverEntry, cancellationToken), cancellationToken);
            coverRelative = library.ToRelative(coverPath);
        }

        string? textRelative = null;
        var textEntry = zip.GetEntry(TextEntry);
        if (textEntry is not null)
        {
            var textBytes = await ReadEntryAsync(textEntry, cancellationToken);
            if (manifest.Encrypted)
            {
                textBytes = _cipher.Decrypt(textBytes);
            }
            var textPath = Path.Combine(dir, "content.txt");
            await File.WriteAllBytesAsync(textPath, textBytes, cancellationToken);
            textRelative = library.ToRelative(textPath);
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);
        if (book is not null)
        {
            book.CoverPath = coverRelative ?? book.CoverPath;
            book.TextPath = textRelative ?? book.TextPath;
            book.SourcePackageId = manifest.PackageId;
            if (textRelative is not null)
            {
                book.IsScanned = false;
                book.OcrStatus = OcrStatus.Completed;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<OperationResult<string>> ExportCatalogAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var books = await db.Books.AsNoTracking().ToListAsync(cancellationToken);

        var catalog = new CatalogBundle
        {
            FormatVersion = _packages.Value.FormatVersion,
            LibraryName = _library.Value.LibraryName,
            ExportedAt = DateTimeOffset.UtcNow,
            Books = books.Select(b => new CatalogEntry
            {
                Title = b.Title,
                Author = b.Author,
                Publisher = b.Publisher,
                PublicationYear = b.PublicationYear,
                Isbn = b.Isbn,
                Language = b.Language,
                PageCount = b.PageCount
            }).ToList()
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(catalog, JsonOptions);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(_library.Value.PackagesPath);
            outputPath = Path.Combine(_library.Value.PackagesPath, $"catalog-{DateTime.UtcNow:yyyyMMddHHmmss}{_packages.Value.CatalogExtension}");
        }

        if (_packages.Value.Sign && !_signer.HasPrivateKey)
        {
            _keyManager.RegenerateSigningKey();
        }

        await using var fileStream = File.Create(outputPath);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);

        var payload = _packages.Value.Encrypt ? _cipher.Encrypt(json) : json;
        await WriteEntryAsync(zip, "catalog.bin", payload, cancellationToken);
        if (_packages.Value.Sign)
        {
            await WriteEntryAsync(zip, SignatureEntry, _signer.Sign(json), cancellationToken);
        }
        await WriteEntryAsync(zip, "manifest.json", json, cancellationToken);

        return OperationResult<string>.Ok(outputPath);
    }

    private async Task RecordPackageAsync(PackageManifest manifest, string path, Guid bookId, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        db.Packages.Add(new PackageRecord
        {
            PackageId = manifest.PackageId,
            FileName = Path.GetFileName(path),
            BookId = bookId,
            Version = manifest.PackageVersion,
            SizeBytes = new FileInfo(path).Length,
            Checksum = manifest.ContentSha256,
            IsInstalled = true
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private string BookTextPath(Guid bookId) => Path.Combine(_library.Value.BookDirectory(bookId), "content.txt");

    private static async Task WriteEntryAsync(ZipArchive zip, string name, byte[] data, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(data, cancellationToken);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}

public class CatalogBundle
{
    public int FormatVersion { get; set; }
    public string LibraryName { get; set; } = string.Empty;
    public DateTimeOffset ExportedAt { get; set; }
    public List<CatalogEntry> Books { get; set; } = new();
}

public class CatalogEntry
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? Isbn { get; set; }
    public string? Language { get; set; }
    public int PageCount { get; set; }
}
