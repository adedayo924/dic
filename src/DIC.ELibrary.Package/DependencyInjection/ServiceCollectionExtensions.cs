using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Package;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.Package.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddELibraryPackage(this IServiceCollection services)
    {
        services.AddSingleton<IPackageService, DicpkgService>();
        return services;
    }
}
