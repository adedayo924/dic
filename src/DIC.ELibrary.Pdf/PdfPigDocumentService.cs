using System.Text;
using DIC.ELibrary.Core.Abstractions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DIC.ELibrary.Pdf;

/// <summary>Metadata and embedded-text extraction backed by PdfPig (cross-platform, managed).</summary>
public sealed class PdfPigDocumentService : IPdfService
{
    private readonly ILogger<PdfPigDocumentService> _logger;

    public PdfPigDocumentService(ILogger<PdfPigDocumentService> logger) => _logger = logger;

    public Task<PdfInfo> ReadInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var document = PdfDocument.Open(filePath);
            var info = document.Information;

            var sample = new StringBuilder();
            var pagesToSample = Math.Min(document.NumberOfPages, 5);
            for (var i = 1; i <= pagesToSample; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sample.Append(document.GetPage(i).Text);
            }

            return new PdfInfo
            {
                Title = NullIfBlank(info.Title),
                Author = NullIfBlank(info.Author),
                Subject = NullIfBlank(info.Subject),
                PageCount = document.NumberOfPages,
                ExtractedTextLength = sample.Length
            };
        }, cancellationToken);
    }

    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var document = PdfDocument.Open(filePath);
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine(page.Text);
            }
            return sb.ToString();
        }, cancellationToken);
    }

    public Task<string> ExtractPageTextAsync(string filePath, int pageNumber, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var document = PdfDocument.Open(filePath);
            if (pageNumber < 1 || pageNumber > document.NumberOfPages)
            {
                return string.Empty;
            }
            return document.GetPage(pageNumber).Text;
        }, cancellationToken);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
