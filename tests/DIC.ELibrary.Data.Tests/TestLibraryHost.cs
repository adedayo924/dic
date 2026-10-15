using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.DependencyInjection;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.DependencyInjection;
using DIC.ELibrary.Data.Persistence;
using DIC.ELibrary.Security.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DIC.ELibrary.Data.Tests;

/// <summary>
/// Spins up an isolated local library (temp data directory + real SQLite database)
/// using the same composition root as the applications.
/// </summary>
public sealed class TestLibraryHost : IAsyncDisposable
{
    public string DataDirectory { get; }
    public ServiceProvider Services { get; }

    private TestLibraryHost(string dataDirectory, ServiceProvider services)
    {
        DataDirectory = dataDirectory;
        Services = services;
    }

    public static async Task<TestLibraryHost> CreateAsync(bool initialize = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dic-eltests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddELibraryCore();
        services.Configure<LibraryOptions>(options =>
        {
            options.DataDirectory = directory;
            options.DefaultLoanDays = 14;
            options.MaxConcurrentLoans = 0;
        });
        services.Configure<SecurityOptions>(options =>
        {
            options.KeyStoreDirectory = Path.Combine(directory, "keys");
            options.PasswordIterations = 1_000;
        });
        services.Configure<OcrOptions>(_ => { });
        services.Configure<PackageOptions>(_ => { });
        services.AddELibrarySecurity();
        services.AddELibraryData();
        services.AddSingleton<IPdfService, StubPdfService>();

        var provider = services.BuildServiceProvider();
        var host = new TestLibraryHost(directory, provider);

        if (initialize)
        {
            await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        }

        return host;
    }

    public T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        try
        {
            if (Directory.Exists(DataDirectory))
            {
                Directory.Delete(DataDirectory, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}

/// <summary>Deterministic stand-in for the PdfPig-backed document service.</summary>
internal sealed class StubPdfService : IPdfService
{
    public Task<PdfInfo> ReadInfoAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PdfInfo
        {
            Title = "Stub Title",
            Author = "Stub Author",
            PageCount = 3,
            ExtractedTextLength = 120
        });

    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult("This is the stubbed extracted body text.");

    public Task<string> ExtractPageTextAsync(string filePath, int pageNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult("stub page text");

    public Task<IReadOnlyList<PdfTextMatch>> SearchAsync(string filePath, string term, int maxMatches = 200, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PdfTextMatch>>(Array.Empty<PdfTextMatch>());
}
