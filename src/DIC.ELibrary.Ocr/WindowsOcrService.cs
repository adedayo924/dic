using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DIC.ELibrary.Ocr;

/// <summary>OCR backed by the built-in Windows.Media.Ocr engine (no external dependencies).</summary>
public sealed class WindowsOcrService : IOcrService
{
    private readonly OcrOptions _options;
    private readonly ILogger<WindowsOcrService> _logger;

    public WindowsOcrService(IOptions<OcrOptions> options, ILogger<WindowsOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                return CreateEngine() is not null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Windows OCR engine is unavailable.");
                return false;
            }
        }
    }

    public IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            try
            {
                return OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    public async Task<OcrPageResult> RecognizeImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var engine = CreateEngine() ?? throw new InvalidOperationException("Windows OCR is not available on this machine.");
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
        var text = await RecognizeFileAsync(engine, file, cancellationToken);
        return new OcrPageResult { PageNumber = 1, Text = text, Confidence = text.Length > 0 ? 1 : 0 };
    }

    public async Task<IReadOnlyList<OcrPageResult>> RecognizePdfAsync(string pdfFilePath, CancellationToken cancellationToken = default)
    {
        var engine = CreateEngine() ?? throw new InvalidOperationException("Windows OCR is not available on this machine.");
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pdfFilePath));
        var pdf = await PdfDocument.LoadFromFileAsync(file);

        var results = new List<OcrPageResult>();
        var pageCount = _options.MaxPages > 0 ? Math.Min(_options.MaxPages, (int)pdf.PageCount) : (int)pdf.PageCount;

        for (uint i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var page = pdf.GetPage(i);
                using var stream = new InMemoryRandomAccessStream();
                var renderOptions = new PdfPageRenderOptions { DestinationWidth = 2000 };
                await page.RenderToStreamAsync(stream, renderOptions);
                stream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(stream);
                using var bitmap = await decoder.GetSoftwareBitmapAsync();
                var ocr = await engine.RecognizeAsync(bitmap);
                results.Add(new OcrPageResult
                {
                    PageNumber = (int)i + 1,
                    Text = ocr.Text ?? string.Empty,
                    Confidence = string.IsNullOrWhiteSpace(ocr.Text) ? 0 : 1
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCR failed for page {Page} of {File}.", i + 1, pdfFilePath);
                results.Add(new OcrPageResult { PageNumber = (int)i + 1, Text = string.Empty, Confidence = 0 });
            }
        }

        return results;
    }

    private async Task<string> RecognizeFileAsync(OcrEngine engine, StorageFile file, CancellationToken cancellationToken)
    {
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        var result = await engine.RecognizeAsync(bitmap);
        return result.Text ?? string.Empty;
    }

    private OcrEngine? CreateEngine()
    {
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            var language = new Language(_options.Language);
            if (OcrEngine.IsLanguageSupported(language))
            {
                return OcrEngine.TryCreateFromLanguage(language);
            }
            _logger.LogWarning("OCR language '{Language}' is not installed; falling back to user profile languages.", _options.Language);
        }
        return OcrEngine.TryCreateFromUserProfileLanguages();
    }
}
