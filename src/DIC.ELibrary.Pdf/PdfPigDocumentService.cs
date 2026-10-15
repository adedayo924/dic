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

    public Task<IReadOnlyList<PdfTextMatch>> SearchAsync(string filePath, string term, int maxMatches = 200, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<PdfTextMatch>>(() =>
        {
            var results = new List<PdfTextMatch>();
            if (string.IsNullOrWhiteSpace(term))
            {
                return results;
            }

            using var document = PdfDocument.Open(filePath);
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = page.Text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var count = CountOccurrences(text, term);
                if (count == 0)
                {
                    continue;
                }

                results.Add(new PdfTextMatch
                {
                    PageNumber = page.Number,
                    Snippet = BuildSnippet(text, term),
                    MatchCount = count
                });

                if (results.Count >= Math.Max(1, maxMatches))
                {
                    break;
                }
            }

            return results;
        }, cancellationToken);
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    private static string BuildSnippet(string text, string term)
    {
        const int radius = 40;
        var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return string.Empty;
        }

        var start = Math.Max(0, index - radius);
        var end = Math.Min(text.Length, index + term.Length + radius);
        var snippet = text.Substring(start, end - start).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (start > 0)
        {
            snippet = "..." + snippet;
        }
        if (end < text.Length)
        {
            snippet += "...";
        }
        return snippet;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
