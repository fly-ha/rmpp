using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Printing.Windows.Rendering;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using SkiaSharp;
using System.IO;
using System.Windows.Media.Imaging;
using Xunit;

namespace Rmpp.Desktop.Tests.Conformance;

public sealed class BackendConformanceTests
{
    [Fact]
    public async Task PreviewPdfAndWpfPrintPreservePhysicalRectangleWithinTwoPixels()
    {
        const double dpi = 96;
        RenderPage page = CreatePage();
        PixelBounds preview;
        using (SKBitmap bitmap = new SkiaBitmapRenderer().Render(page, dpi)) preview = BoundsFromSkia(bitmap);

        await using MemoryStream pdf = new();
        new SkiaPdfExporter().Export(new RenderScene { DocumentId = Guid.NewGuid(), Pages = [page] }, pdf);
        int width = checked((int)Math.Ceiling(page.Size.Width * dpi / 25.4));
        int height = checked((int)Math.Ceiling(page.Size.Height * dpi / 25.4));
        PdfRasterizedPage rasterized = await new PdfBackgroundRasterizer().RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = pdf.ToArray(),
            PageNumber = 1,
            TargetWidthPixels = width,
            TargetHeightPixels = height,
        });
        PixelBounds pdfBounds = BoundsFromBgra(rasterized.Pixels.ToArray(), rasterized.Width, rasterized.Height, rasterized.Stride);
        PixelBounds printBounds = RenderWpf(page, width, height);

        AssertClose(preview, pdfBounds, 2);
        AssertClose(preview, printBounds, 2);
        double expectedWidth = 10 * dpi / 25.4;
        Assert.InRange(preview.Width, expectedWidth - 2, expectedWidth + 2);
    }

    private static RenderPage CreatePage()
    {
        RenderPath path = new RenderPathBuilder()
            .MoveTo(new MmPoint(5, 5)).LineTo(new MmPoint(15, 5)).LineTo(new MmPoint(15, 15)).LineTo(new MmPoint(5, 15)).Close().Build();
        return new RenderPage
        {
            PageNumber = 1,
            Size = new MmSize(30, 30),
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, 30, 30)),
            Commands =
            [
                new RenderPathCommand
                {
                    SourceId = Guid.NewGuid(),
                    Path = path,
                    Stroke = RenderStroke.FromDomain(StrokeStyle.None),
                    Fill = new RenderSolidFill(RgbaColor.Black),
                },
            ],
        };
    }

    private static PixelBounds RenderWpf(RenderPage page, int width, int height)
    {
        PixelBounds result = default;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                WpfRenderedPage rendered = new WpfPrintSceneRenderer().Render(page);
                RenderTargetBitmap target = new(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                target.Render(rendered.Visual);
                int stride = width * 4;
                byte[] pixels = new byte[stride * height];
                target.CopyPixels(pixels, stride, 0);
                result = BoundsFromBgra(pixels, width, height, stride);
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        return result;
    }

    private static PixelBounds BoundsFromSkia(SKBitmap bitmap)
    {
        List<(int X, int Y)> dark = [];
        for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
        {
            SKColor color = bitmap.GetPixel(x, y);
            if (color.Red < 128 && color.Green < 128 && color.Blue < 128) dark.Add((x, y));
        }
        return Bounds(dark);
    }

    private static PixelBounds BoundsFromBgra(byte[] pixels, int width, int height, int stride)
    {
        List<(int X, int Y)> dark = [];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int offset = y * stride + x * 4;
            if (pixels[offset + 3] > 128 && pixels[offset] < 128 && pixels[offset + 1] < 128 && pixels[offset + 2] < 128) dark.Add((x, y));
        }
        return Bounds(dark);
    }

    private static PixelBounds Bounds(List<(int X, int Y)> points)
    {
        Assert.NotEmpty(points);
        int left = points.Min(static point => point.X);
        int top = points.Min(static point => point.Y);
        int right = points.Max(static point => point.X);
        int bottom = points.Max(static point => point.Y);
        return new PixelBounds(left, top, right, bottom);
    }

    private static void AssertClose(PixelBounds expected, PixelBounds actual, int tolerance)
    {
        Assert.InRange(actual.Left, expected.Left - tolerance, expected.Left + tolerance);
        Assert.InRange(actual.Top, expected.Top - tolerance, expected.Top + tolerance);
        Assert.InRange(actual.Right, expected.Right - tolerance, expected.Right + tolerance);
        Assert.InRange(actual.Bottom, expected.Bottom - tolerance, expected.Bottom + tolerance);
    }

    private readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left + 1;
    }
}
