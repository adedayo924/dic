using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using DIC.ELibrary.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddELibraryData(this IServiceCollection services)
    {
        services.AddDbContextFactory<LibraryDbContext>((sp, options) =>
        {
            var library = sp.GetRequiredService<IOptions<LibraryOptions>>().Value;
            Directory.CreateDirectory(library.ResolvedDataDirectory);
            options.UseSqlite($"Data Source={library.DatabasePath}");
            options.EnableSensitiveDataLogging(false);
        });

        services.AddSingleton<DatabaseInitializer>();

        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IBookService, BookService>();
        services.AddSingleton<ILoanService, LoanService>();
        services.AddSingleton<IReaderService, ReaderService>();
        services.AddSingleton<IStatsService, StatsService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IBackupScheduler, BackupScheduler>();
        services.AddSingleton<IReportService, ReportService>();

        return services;
    }
}
