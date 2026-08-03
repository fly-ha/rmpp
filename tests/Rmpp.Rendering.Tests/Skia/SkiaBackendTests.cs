using SkiaSharp;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Barcodes;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Xunit;

namespace Rmpp.Rendering.Tests.Skia;

public sealed class SkiaBackendTests
{
    [Fact]
    public void BitmapUsesExplicitDpiAndPhysicalPageSize()
    {
        RenderPage page = Page(new MmSize(210, 297), [RectangleCommand(new MmRect(10, 10, 20, 20))]);

        using SKBitmap bitmap = new SkiaBitmapRenderer().Render(page, 100);

        Assert.Equal(827, bitmap.Width);
        Assert.Equal(1170, bitmap.Height);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(60, 60));
    }

    [Fact]
    public void BarcodeAndGlyphSceneRendersDeterministically()
    {
        RenderBarcodeCommand barcode = new()
        {
            SourceId = Guid.NewGuid(),
            LocalBounds = new MmRect(0, 0, 50, 20),
            Content = "RMPP-128",
            Symbology = BarcodeSymbology.Code128,
            QuietZoneMm = 1,
            ShowHumanReadableText = false,
            HumanReadableTextStyle = TextStyle(),
        };
        RenderPage page = Page(new MmSize(60, 30), [barcode]);

        using SKBitmap first = new SkiaBitmapRenderer().Render(page, 300);
        using SKBitmap second = new SkiaBitmapRenderer().Render(page, 300);

        Assert.Equal(Hash(first), Hash(second));
        Assert.Contains(Enumerable.Range(0, first.Width), x => first.GetPixel(x, first.Height / 2).Red == 0);
    }

    [Fact]
    public void PdfExporterProducesLocalPdfDocument()
    {
        RenderScene scene = new()
        {
            DocumentId = Guid.NewGuid(),
            Pages = [Page(new MmSize(210, 297), [RectangleCommand(new MmRect(0, 0, 20, 20))])],
        };
        using MemoryStream stream = new();

        new SkiaPdfExporter().Export(scene, stream);

        byte[] bytes = stream.ToArray();
        Assert.True(bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
        Assert.True(bytes.Length > 500);
    }

    [Fact]
    public void ElementOpacityMultipliesTheFillAlpha()
    {
        RenderPathCommand command = RectangleCommand(new MmRect(0, 0, 10, 10)) with
        {
            Opacity = 0.5,
            Fill = new RenderSolidFill(new RgbaColor(255, 0, 0, 128)),
        };
        RenderPage page = Page(new MmSize(10, 10), [command]);

        using SKBitmap bitmap = new SkiaBitmapRenderer().Render(
            page,
            25.4,
            options: new SkiaRenderOptions { PageColor = RgbaColor.Transparent });

        SKColor center = bitmap.GetPixel(5, 5);
        Assert.InRange(center.Alpha, 63, 65);
    }

    private static RenderPage Page(MmSize size, IReadOnlyList<RenderCommand> commands) => new()
    {
        PageNumber = 1,
        Size = size,
        Clip = RenderClip.FromRectangle(new MmRect(0, 0, size.Width, size.Height)),
        Commands = commands,
    };

    private static RenderPathCommand RectangleCommand(MmRect bounds) => new()
    {
        SourceId = Guid.NewGuid(),
        Transform = RenderTransform.Translation(bounds.X, bounds.Y),
        Path = new RenderPathBuilder()
            .MoveTo(new MmPoint(0, 0))
            .LineTo(new MmPoint(bounds.Width, 0))
            .LineTo(new MmPoint(bounds.Width, bounds.Height))
            .LineTo(new MmPoint(0, bounds.Height))
            .Close()
            .Build(),
        Fill = new RenderSolidFill(new RgbaColor(200, 20, 20)),
        Stroke = new RenderStroke { IsEnabled = false },
    };

    private static RenderTextStyle TextStyle() => new()
    {
        FontFamily = "Microsoft YaHei",
        FontSizePoints = 8,
        Color = RgbaColor.Black,
        LineSpacing = 1,
    };

    private static int Hash(SKBitmap bitmap)
    {
        HashCode hash = new();
        for (int y = 0; y < bitmap.Height; y += Math.Max(1, bitmap.Height / 50))
        {
            for (int x = 0; x < bitmap.Width; x += Math.Max(1, bitmap.Width / 50))
            {
                hash.Add(bitmap.GetPixel(x, y));
            }
        }

        return hash.ToHashCode();
    }
}
