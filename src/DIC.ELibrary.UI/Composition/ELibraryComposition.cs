using System.IO;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.DependencyInjection;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.DependencyInjection;
using DIC.ELibrary.Data.Persistence;
using DIC.ELibrary.Ocr.DependencyInjection;
using DIC.ELibrary.Package.DependencyInjection;
using DIC.ELibrary.Pdf.DependencyInjection;
using DIC.ELibrary.Security.DependencyInjection;
using DIC.ELibrary.UI.DependencyInjection;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace DIC.ELibrary.UI.Composition;

/// <summary>
/// Central composition root. Wires configuration, logging (Serilog), every feature layer
/// and the WPF UI into a single generic host shared by both desktop applications.
/// </summary>
public static class ELibraryComposition
{
    public static IHost BuildHost(string[] args, Action<AppShellOptions, IServiceCollection> configureShell)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        // Optional machine-wide settings let an administrator pin shared values (such as
        // Library:DataDirectory) for every user on the machine. Precedence: command line > env
        // vars > this file > appsettings.json.
        var machineConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DIC",
            "ELibrary",
            "appsettings.json");
        builder.Configuration.AddJsonFile(machineConfig, optional: true, reloadOnChange: false);

        var library = builder.Configuration.GetSection(LibraryOptions.SectionName).Get<LibraryOptions>() ?? new LibraryOptions();
        Directory.CreateDirectory(library.ResolvedDataDirectory);
        Directory.CreateDirectory(library.LogsPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(library.LogsPath, "elibrary-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();

        builder.Services.AddSerilog(Log.Logger, dispose: true);

        var shell = new AppShellOptions();
        builder.Services.AddSingleton(shell);

        builder.Services.AddELibraryCore(builder.Configuration);
        builder.Services.AddELibraryData();
        builder.Services.AddELibrarySecurity();
        builder.Services.AddELibraryPdf();
        builder.Services.AddELibraryOcr();
        builder.Services.AddELibraryPackage();
        builder.Services.AddELibraryUi();

        configureShell(shell, builder.Services);

        var host = builder.Build();
        ImageLocator.Initialize(host.Services.GetRequiredService<IOptions<LibraryOptions>>().Value);

        // Apply persisted theme before UI is shown
        try
        {
            using var scope = host.Services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<IAppSettingsService>();
            var theme = scope.ServiceProvider.GetRequiredService<IThemeService>();
            var stored = settings.GetAsync("Theme").GetAwaiter().GetResult();
            theme.Apply(stored);
        }
        catch
        {
            // Ignore theme load failures; fall back to default
        }

        return host;
    }

    public static async Task<DatabaseInitializer> InitializeDatabaseAsync(IHost host, CancellationToken cancellationToken = default)
    {
        var initializer = host.Services.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(cancellationToken);
        return initializer;
    }
}
