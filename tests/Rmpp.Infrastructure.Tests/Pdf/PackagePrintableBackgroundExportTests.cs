using Rmpp.Application.Abstractions;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Infrastructure.Templates;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using SkiaSharp;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Pdf;

public sealed class PackagePrintableBackgroundExportTests
{
    [Fact]
    public async Task PackagedPdfBackgroundIsResolvedDuringLocalPdfExport()
    {
        byte[] pdfBackground = CreatePdfBackground();
        Guid assetId = Guid.NewGuid();
        AssetReference asset = new(
            assetId,
            "form.pdf",
            "application/pdf",
            AssetHashService.ComputeSha256(pdfBackground));
        TemplateDocument document = TemplateDocument.CreateNew("可打印背景") with
        {
            Assets = [asset],
            Backgrounds =
            [
                new BackgroundDefinition
                {
                    AssetId = assetId,
                    Bounds = new MmRect(0, 0, 50, 50),
                    IsPrintable = true,
                    IsVisible = true,
                },
            ],
        };
        Dictionary<Guid, ReadOnlyMemory<byte>> contents = new() { [assetId] = pdfBackground };
        PackageRenderAssetProvider provider = new(document, contents);
        RenderScene scene = new RenderSceneBuilder().Build(document, new RenderContext { Target = RenderTarget.Pdf });

        using MemoryStream output = new();
        await new LocalRenderExporter().ExportAsync(scene, output, new RenderExportOptions
        {
            AssetProvider = provider,
            ImageSourceDpi = 144,
        });

        byte[] exported = output.ToArray();
        Assert.True(exported.Length > 500);
        Assert.Equal("%PDF-"u8.ToArray(), exported[..5]);
    }

    private static byte[] CreatePdfBackground()
    {
        using MemoryStream stream = new();
        using (SKDocument document = SKDocument.CreatePdf(stream))
        {
            SKCanvas canvas = document.BeginPage(200, 200);
            using SKPaint paint = new() { Color = SKColors.DarkRed };
            canvas.DrawRect(10, 10, 180, 180, paint);
            document.EndPage();
            document.Close();
        }

        return stream.ToArray();
    }
}
