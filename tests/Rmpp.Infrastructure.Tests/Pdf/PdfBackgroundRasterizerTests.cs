using SkiaSharp;
using System.Runtime.InteropServices;
using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Rendering.Images;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Pdf;

public sealed class PdfBackgroundRasterizerTests
{
    [Fact]
    public async Task RasterizesRequestedPageWithinPixelLimits()
    {
        byte[] pdf = CreatePdf();
        PdfRasterizedPage page = await new PdfBackgroundRasterizer().RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = pdf,
            PageNumber = 1,
            TargetWidthPixels = 200,
            TargetHeightPixels = 300,
        });

        Assert.InRange(page.Width, 1, 200);
        Assert.InRange(page.Height, 1, 300);
        Assert.Equal(page.Width * 4, page.Stride);
        Assert.Equal(page.Stride * page.Height, page.Pixels.Length);
        Assert.Contains(page.Pixels.Span.ToArray(), static value => value != byte.MaxValue);
    }

    [Fact]
    public async Task InvalidHeaderAndOversizedTargetAreRejected()
    {
        PdfBackgroundRasterizer rasterizer = new(new PdfRasterizationLimits
        {
            MaximumDimensionPixels = 100,
            MaximumPixels = 10_000,
        });

        await Assert.ThrowsAsync<InvalidDataException>(() => rasterizer.RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = "not-pdf"u8.ToArray(),
            PageNumber = 1,
            TargetWidthPixels = 10,
            TargetHeightPixels = 10,
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => rasterizer.RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = CreatePdf(),
            PageNumber = 1,
            TargetWidthPixels = 101,
            TargetHeightPixels = 10,
        }));
    }

    [Fact]
    public async Task PageNumberAndCancellationAreEnforced()
    {
        PdfBackgroundRasterizer rasterizer = new();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rasterizer.RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = CreatePdf(),
            PageNumber = 2,
            TargetWidthPixels = 100,
            TargetHeightPixels = 100,
        }));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rasterizer.RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = CreatePdf(),
            PageNumber = 1,
            TargetWidthPixels = 100,
            TargetHeightPixels = 100,
        }, cancellation.Token));
    }

    [Fact]
    public async Task RasterizedPdfPageCanServeAsSkiaBackground()
    {
        PdfRasterizedPage rasterized = await new PdfBackgroundRasterizer().RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = CreatePdf(),
            PageNumber = 1,
            TargetWidthPixels = 200,
            TargetHeightPixels = 300,
        });
        RenderImageCommand background = new()
        {
            SourceId = Guid.NewGuid(),
            LocalBounds = new MmRect(0, 0, 100, 150),
            Image = new RenderImage { AssetId = Guid.NewGuid(), FitMode = ImageFitMode.Stretch, PdfPageNumber = 1 },
            Border = new RenderStroke { IsEnabled = false },
            Fill = new RenderNoFill(),
        };
        RenderPage page = new()
        {
            PageNumber = 1,
            Size = new MmSize(100, 150),
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, 100, 150)),
            Commands = [background],
        };

        using SKBitmap bitmap = new SkiaBitmapRenderer().Render(page, 96, new PdfPageAssetProvider(rasterized));

        Assert.Contains(Enumerable.Range(0, bitmap.Height), y => Enumerable.Range(0, bitmap.Width)
            .Any(x => bitmap.GetPixel(x, y).Red > bitmap.GetPixel(x, y).Blue));
    }

    private static byte[] CreatePdf()
    {
        using MemoryStream stream = new();
        using (SKDocument document = SKDocument.CreatePdf(stream))
        {
            SKCanvas canvas = document.BeginPage(200, 300);
            using SKPaint paint = new() { Color = SKColors.DarkRed };
            canvas.DrawRect(20, 30, 80, 90, paint);
            document.EndPage();
            document.Close();
        }

        return stream.ToArray();
    }

    private sealed class PdfPageAssetProvider(PdfRasterizedPage page) : IRenderAssetProvider
    {
        public DecodedImage? Load(RenderImage image, double targetDpi)
        {
            SKBitmap bitmap = new(new SKImageInfo(page.Width, page.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            Marshal.Copy(page.Pixels.ToArray(), 0, bitmap.GetPixels(), page.Pixels.Length);
            return new DecodedImage(bitmap);
        }
    }
}
