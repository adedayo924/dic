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

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dic-pdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

/// <summary>Builds a minimal, spec-valid single-page PDF containing the supplied text.</summary>
internal static class MinimalPdf
{
    public static byte[] Create(string text)
    {
        var stream = $"BT /F1 18 Tf 20 100 Td ({text}) Tj ET";
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append(i + 1).Append(" 0 obj\n").Append(bodies[i]).Append("\nendobj\n");
        }

        var xrefOffset = sb.Length;
        sb.Append("xref\n0 ").Append(bodies.Length + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        sb.Append("trailer\n<< /Size ").Append(bodies.Length + 1).Append(" /Root 1 0 R >>\n");
        sb.Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
