namespace DIC.ELibrary.Core.Options;

public class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>BCP-47 language tag passed to the Windows OCR engine (e.g. "en-US", "bn-BD"). Empty = engine default.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Safety limit on how many pages are OCR'd per document. 0 = all pages.</summary>
    public int MaxPages { get; set; } = 0;

    /// <summary>Do not attempt OCR on documents that already contain extractable text.</summary>
    public bool SkipIfTextPresent { get; set; } = true;

    /// <summary>Minimum number of characters of embedded text required to consider a PDF "digital".</summary>
    public int TextDetectionThreshold { get; set; } = 100;
}
