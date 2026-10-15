using System.Text;
using System.Text.RegularExpressions;
using DIC.ELibrary.Pdf;
using Microsoft.Extensions.Logging.Abstractions;

namespace DIC.ELibrary.Pdf.Tests;

public class PdfPigDocumentServiceTests
{
    private readonly PdfPigDocumentService _service = new(NullLogger<PdfPigDocumentService>.Instance);

    [Fact]
    public async Task ReadInfo_OnGeneratedPdf_ReportsSinglePage()
    {
        var path = WriteTempFile(MinimalPdf.Create("Hello World"));
        try
        {
            var info = await _service.ReadInfoAsync(path);

            Assert.Equal(1, info.PageCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractText_ReturnsEmbeddedText()
    {
        var path = WriteTempFile(MinimalPdf.Create("Hello PdfPig"));
        try
        {
            var text = await _service.ExtractTextAsync(path);
            var compact = Regex.Replace(text, @"\s+", string.Empty);

            Assert.Contains("Hello", compact);
            Assert.Contains("PdfPig", compact);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractPageText_OutOfRange_ReturnsEmpty()
    {
        var path = WriteTempFile(MinimalPdf.Create("Only Page"));
        try
        {
            Assert.Equal(string.Empty, await _service.ExtractPageTextAsync(path, 5));
            Assert.Equal(string.Empty, await _service.ExtractPageTextAsync(path, 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadInfo_OnInvalidFile_Throws()
    {
        var path = WriteTempFile(Encoding.ASCII.GetBytes("this is definitely not a pdf"));

        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => _service.ReadInfoAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Search_FindsMatchesAcrossPages()
    {
        var path = WriteTempFile(MinimalPdf.CreateMultiPage("alpha beta", "beta gamma", "nothing here"));
        try
        {
            var matches = await _service.SearchAsync(path, "beta");

            Assert.Equal(2, matches.Count);
            Assert.Equal(1, matches[0].PageNumber);
            Assert.Equal(2, matches[1].PageNumber);
            Assert.All(matches, m => Assert.Equal(1, m.MatchCount));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var path = WriteTempFile(MinimalPdf.Create("Hello World"));
        try
        {
            var matches = await _service.SearchAsync(path, "world");

            Assert.Single(matches);
            Assert.Equal(1, matches[0].PageNumber);
            Assert.Contains("World", matches[0].Snippet);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmpty()
    {
        var path = WriteTempFile(MinimalPdf.Create("alpha beta"));
        try
        {
            Assert.Empty(await _service.SearchAsync(path, "zzz"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Search_EmptyTerm_ReturnsEmpty()
    {
        var path = WriteTempFile(MinimalPdf.Create("alpha beta"));
        try
        {
            Assert.Empty(await _service.SearchAsync(path, "   "));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Search_CountsMultipleOccurrencesOnOnePage()
    {
        var path = WriteTempFile(MinimalPdf.Create("abcabcabc"));
        try
        {
            var matches = await _service.SearchAsync(path, "abc");

            Assert.Single(matches);
            Assert.Equal(3, matches[0].MatchCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dic-pdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

/// <summary>Builds minimal, spec-valid single- or multi-page PDFs containing the supplied text.</summary>
internal static class MinimalPdf
{
    public static byte[] Create(string text) => CreateMultiPage(text);

    public static byte[] CreateMultiPage(params string[] pages)
    {
        var count = pages.Length;
        var bodies = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [" + string.Join(" ", Enumerable.Range(0, count).Select(i => $"{3 + i * 3} 0 R")) + "] /Count " + count + " >>"
        };

        for (var i = 0; i < count; i++)
        {
            var contentObj = 4 + i * 3;
            var fontObj = 5 + i * 3;
            var stream = $"BT /F1 18 Tf 20 100 Td ({Escape(pages[i])}) Tj ET";
            bodies.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 {fontObj} 0 R >> >> /Contents {contentObj} 0 R >>");
            bodies.Add($"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream");
            bodies.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        }

        return Assemble(bodies);
    }

    private static byte[] Assemble(IReadOnlyList<string> bodies)
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(sb.Length);
            sb.Append(i + 1).Append(" 0 obj\n").Append(bodies[i]).Append("\nendobj\n");
        }

        var xrefOffset = sb.Length;
        sb.Append("xref\n0 ").Append(bodies.Count + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        sb.Append("trailer\n<< /Size ").Append(bodies.Count + 1).Append(" /Root 1 0 R >>\n");
        sb.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
