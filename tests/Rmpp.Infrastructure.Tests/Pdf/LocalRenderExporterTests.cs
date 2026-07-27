using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Rendering.Scene;
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
}
