using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Core.DependencyInjection;

/// <summary>
/// Registers core (platform-agnostic) services. Feature layers add their own
/// registrations on top of this via their own extension methods.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddELibraryCore(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUser, CurrentUser>();

        if (configuration is not null)
        {
            services.Configure<LibraryOptions>(configuration.GetSection(LibraryOptions.SectionName));
            services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
            services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
            services.Configure<PackageOptions>(configuration.GetSection(PackageOptions.SectionName));
        }

        services.AddOptions<LibraryOptions>();
        services.AddOptions<SecurityOptions>();
        services.AddOptions<OcrOptions>();
        services.AddOptions<PackageOptions>();

        return services;
    }
}
