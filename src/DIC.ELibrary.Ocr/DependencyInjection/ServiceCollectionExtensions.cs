using DIC.ELibrary.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.Ocr.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddELibraryOcr(this IServiceCollection services)
    {
        services.AddSingleton<IOcrService, WindowsOcrService>();
        services.AddSingleton<IPdfRenderer, WindowsPdfRenderer>();
        return services;
    }
}
