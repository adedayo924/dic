using DIC.ELibrary.Core.Abstractions;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DIC.ELibrary.Ocr;

/// <summary>Renders PDF pages to PNG using the built-in Windows.Data.Pdf engine.</summary>
public sealed class WindowsPdfRenderer : IPdfRenderer
{
    public async Task<int> GetPageCountAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
        var pdf = await PdfDocument.LoadFromFileAsync(file);
        return (int)pdf.PageCount;
    }

    public async Task<byte[]> RenderPagePngAsync(string filePath, int pageNumber, int width, CancellationToken cancellationToken = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
        var pdf = await PdfDocument.LoadFromFileAsync(file);
        if (pageNumber < 1 || pageNumber > pdf.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        using var page = pdf.GetPage((uint)(pageNumber - 1));
        using var stream = new InMemoryRandomAccessStream();
        var options = new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(50, width) };
        await page.RenderToStreamAsync(stream, options);
        return await ReadStreamAsync(stream);
    }

    public Task<byte[]> GenerateCoverAsync(string filePath, int width, CancellationToken cancellationToken = default)
        => RenderPagePngAsync(filePath, 1, width, cancellationToken);

    private static async Task<byte[]> ReadStreamAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);
        var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[stream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
