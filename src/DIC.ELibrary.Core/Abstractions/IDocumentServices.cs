namespace DIC.ELibrary.Core.Abstractions;

public class PdfInfo
{
    public string? Title { get; init; }
    public string? Author { get; init; }
    public string? Subject { get; init; }
    public int PageCount { get; init; }
    public int ExtractedTextLength { get; init; }
    public bool HasText => ExtractedTextLength >= 20;
}

public interface IPdfService
{
    /// <summary>Reads document metadata and probes for embedded text.</summary>
    Task<PdfInfo> ReadInfoAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Extracts all embedded text from the document (digitally-born PDFs).</summary>
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Extracts text from a single page (1-based page number).</summary>
    Task<string> ExtractPageTextAsync(string filePath, int pageNumber, CancellationToken cancellationToken = default);
}

/// <summary>Renders PDF pages to images (Windows-only implementation).</summary>
public interface IPdfRenderer
{
    Task<int> GetPageCountAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Renders a page (1-based) to PNG bytes at the requested pixel width.</summary>
    Task<byte[]> RenderPagePngAsync(string filePath, int pageNumber, int width, CancellationToken cancellationToken = default);

    /// <summary>Renders the first page as a cover image.</summary>
    Task<byte[]> GenerateCoverAsync(string filePath, int width, CancellationToken cancellationToken = default);
}

public class OcrPageResult
{
    public int PageNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public double Confidence { get; init; }
}

public interface IOcrService
{
    bool IsAvailable { get; }
    IReadOnlyList<string> AvailableLanguages { get; }
    Task<OcrPageResult> RecognizeImageAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OcrPageResult>> RecognizePdfAsync(string pdfFilePath, CancellationToken cancellationToken = default);
}
