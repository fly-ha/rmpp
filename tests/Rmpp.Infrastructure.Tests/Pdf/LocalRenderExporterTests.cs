using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Rendering.Scene;
using Docnet.Core;
using Docnet.Core.Models;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Pdf;

public sealed class LocalRenderExporterTests
{
    [Fact]
    public async Task ExportsSelectedPagesWithoutVirtualPrinter()
    {
        RenderScene scene = new()
        {
            DocumentId = Guid.NewGuid(),
            Pages =
            [
                new RenderPage { PageNumber = 1, Size = new MmSize(20, 20), Clip = RenderClip.FromRectangle(new MmRect(0, 0, 20, 20)), Commands = Array.Empty<RenderCommand>() },
                new RenderPage { PageNumber = 2, Size = new MmSize(30, 30), Clip = RenderClip.FromRectangle(new MmRect(0, 0, 30, 30)), Commands = Array.Empty<RenderCommand>() },
            ],
        };
        await using MemoryStream output = new();
        await new LocalRenderExporter().ExportAsync(scene, output, new RenderExportOptions { FirstPage = 2, LastPage = 2 });

        Assert.True(output.Length > 20);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(output.ToArray(), 0, 4));
    }

    [Fact]
    public async Task ExportedMultiPagePdfCanBeReopenedByPdfium()
    {
        RenderScene scene = new()
        {
            DocumentId = Guid.NewGuid(),
            Pages = Enumerable.Range(1, 3).Select(pageNumber => new RenderPage
            {
                PageNumber = pageNumber,
                Size = new MmSize(210, 297),
                Clip = RenderClip.FromRectangle(new MmRect(0, 0, 210, 297)),
                Commands = Array.Empty<RenderCommand>(),
            }).ToArray(),
        };
        await using MemoryStream output = new();

        await new LocalRenderExporter().ExportAsync(scene, output, new RenderExportOptions());

        using var reader = DocLib.Instance.GetDocReader(output.ToArray(), new PageDimensions(100, 100));
        Assert.Equal(3, reader.GetPageCount());
        for (int index = 0; index < reader.GetPageCount(); index++)
        {
            using var page = reader.GetPageReader(index);
            Assert.True(page.GetPageWidth() > 0);
            Assert.True(page.GetPageHeight() > 0);
        }
    }

    [Fact]
    public async Task VerifierRejectsTruncatedPdfAndAcceptsCompletedPdf()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rmpp-pdf-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, "%PDF-broken"u8.ToArray());
            await Assert.ThrowsAsync<InvalidDataException>(() => PdfDocumentVerifier.VerifyAsync(path, 1));

            RenderPage page = new()
            {
                PageNumber = 1,
                Size = new MmSize(20, 20),
                Clip = RenderClip.FromRectangle(new MmRect(0, 0, 20, 20)),
                Commands = Array.Empty<RenderCommand>(),
            };
            await using (FileStream output = new(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await new LocalRenderExporter().ExportAsync(
                    new RenderScene { DocumentId = Guid.NewGuid(), Pages = [page] },
                    output,
                    new RenderExportOptions());
            }

            await PdfDocumentVerifier.VerifyAsync(path, 1);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
