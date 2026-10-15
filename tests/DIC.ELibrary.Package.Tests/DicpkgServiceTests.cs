using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.DependencyInjection;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.DependencyInjection;
using DIC.ELibrary.Data.Persistence;
using DIC.ELibrary.Package.DependencyInjection;
using DIC.ELibrary.Security.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DIC.ELibrary.Package.Tests;

public sealed class DicpkgServiceTests : IAsyncLifetime
{
    private string _directory = string.Empty;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _directory = Path.Combine(Path.GetTempPath(), "dic-eltests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddELibraryCore();
        services.Configure<LibraryOptions>(options => options.DataDirectory = _directory);
        services.Configure<SecurityOptions>(options =>
        {
            options.KeyStoreDirectory = Path.Combine(_directory, "keys");
            options.PasswordIterations = 1_000;
        });
        services.Configure<PackageOptions>(_ => { });
        services.Configure<OcrOptions>(_ => { });
        services.AddELibrarySecurity();
        services.AddELibraryData();
        services.AddELibraryPackage();
        services.AddSingleton<IPdfService, StubPdfService>();

        _provider = services.BuildServiceProvider();
        await _provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private T GetService<T>() where T : notnull => _provider.GetRequiredService<T>();

    private async Task<Guid> CreateBookAsync(string title = "Packaged Title")
    {
        var pdf = Path.Combine(Path.GetTempPath(), $"dic-test-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(pdf, "%PDF-1.4 dummy content used for packaging");
        try
        {
            var created = await GetService<IBookService>().CreateAsync(
                new BookEditModel { Title = title, Author = "Packager", Status = BookStatus.Published },
                pdf);
            Assert.True(created.Succeeded);
            return created.Value!.Id;
        }
        finally
        {
            File.Delete(pdf);
        }
    }

    [Fact]
    public async Task Build_ProducesSignedPackage_ThatInspectsValidly()
    {
        var service = GetService<IPackageService>();
        var bookId = await CreateBookAsync();
        var outputPath = Path.Combine(_directory, "out", "book.dicpkg");

        var build = await service.BuildAsync(bookId, outputPath);

        Assert.True(build.Succeeded);
        Assert.True(File.Exists(outputPath));

        var inspect = await service.InspectAsync(outputPath);
        Assert.True(inspect.Succeeded);
        Assert.True(inspect.Value!.IsValid);
        Assert.True(inspect.Value.SignatureVerified);
        Assert.Equal("Packaged Title", inspect.Value.Manifest!.Title);
        Assert.True(inspect.Value.Manifest.ContentEncrypted);
    }

    [Fact]
    public async Task Build_ForMissingBook_Fails()
    {
        var service = GetService<IPackageService>();

        var result = await service.BuildAsync(Guid.NewGuid(), Path.Combine(_directory, "nope.dicpkg"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Inspect_MissingFile_Fails()
    {
        var service = GetService<IPackageService>();

        var result = await service.InspectAsync(Path.Combine(_directory, "does-not-exist.dicpkg"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Inspect_CorruptFile_IsInvalid()
    {
        var service = GetService<IPackageService>();
        var corrupt = Path.Combine(_directory, "corrupt.dicpkg");
        await File.WriteAllBytesAsync(corrupt, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = await service.InspectAsync(corrupt);

        Assert.False(result.Value!.IsValid);
    }

    [Fact]
    public async Task Install_AddsNewBook_WithSourcePackage()
    {
        var service = GetService<IPackageService>();
        var books = GetService<IBookService>();
        var bookId = await CreateBookAsync("Round Trip");
        var outputPath = Path.Combine(_directory, "roundtrip.dicpkg");
        Assert.True((await service.BuildAsync(bookId, outputPath)).Succeeded);

        var install = await service.InstallAsync(outputPath);

        Assert.True(install.Succeeded);
        Assert.NotEqual(bookId, install.Value);

        var installed = await books.GetAsync(install.Value);
        Assert.NotNull(installed);
        Assert.Equal("Round Trip", installed!.Title);

        var all = await books.QueryAsync(new BookQuery { PageSize = 50 });
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task ExportCatalog_ProducesFile()
    {
        var service = GetService<IPackageService>();
        await CreateBookAsync("Catalog Entry");
        var outputPath = Path.Combine(_directory, "catalog.dicl");

        var result = await service.ExportCatalogAsync(outputPath);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(outputPath));
    }
}

internal sealed class StubPdfService : IPdfService
{
    public Task<PdfInfo> ReadInfoAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PdfInfo { Title = "Stub", Author = "Stub", PageCount = 1, ExtractedTextLength = 0 });

    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<string> ExtractPageTextAsync(string filePath, int pageNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);

    public Task<IReadOnlyList<PdfTextMatch>> SearchAsync(string filePath, string term, int maxMatches = 200, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PdfTextMatch>>(Array.Empty<PdfTextMatch>());
}
