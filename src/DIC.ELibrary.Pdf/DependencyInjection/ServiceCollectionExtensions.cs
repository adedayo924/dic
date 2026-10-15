using DIC.ELibrary.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DIC.ELibrary.Pdf.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddELibraryPdf(this IServiceCollection services)
    {
        services.AddSingleton<IPdfService, PdfPigDocumentService>();
        return services;
    }
}
