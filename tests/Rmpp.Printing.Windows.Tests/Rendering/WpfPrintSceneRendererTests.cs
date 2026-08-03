using System.Windows;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Domain.Elements;
using Rmpp.Printing.Windows.Rendering;
using Rmpp.Rendering.Images;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using SkiaSharp;
using Xunit;

namespace Rmpp.Printing.Windows.Tests.Rendering;

public sealed class WpfPrintSceneRendererTests
{
    [Fact]
    public void RendersVectorPathAtPhysicalDipSizeOnStaThread()
    {
        Exception? failure = null;
        WpfRenderedPage? result = null;
        Thread thread = new(() =>
        {
            try
            {
                RenderPath path = new RenderPathBuilder()
                    .MoveTo(new MmPoint(0, 0))
                    .LineTo(new MmPoint(50, 0))
                    .Build();
                RenderPage page = new()
                {
                    PageNumber = 1,
                    Size = new MmSize(100, 50),
                    Clip = RenderClip.FromRectangle(new MmRect(0, 0, 100, 50)),
                    Commands =
                    [
                        new RenderPathCommand
                        {
                            SourceId = Guid.NewGuid(),
                            Path = path,
                            Stroke = new RenderStroke { IsEnabled = true, WidthMm = 0.2, Color = RgbaColor.Black },
                            Fill = new RenderNoFill(),
                        },
                    ],
                };
                result = new WpfPrintSceneRenderer().Render(page);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.Equal(100d / 25.4 * 96, result.Size.Width, 6);
        Assert.Equal(50d / 25.4 * 96, result.Size.Height, 6);
    }

    [Fact]
    public void RasterFallbackRejectsUnboundedPixelSurface()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RasterFallbackRenderer.Render(new System.Windows.Media.DrawingVisual(), new MmSize(1000, 1000), 2400, 1_000));
    }

    [Fact]
    public void RendersQrCenterIconThroughSharedAssetProviderOnStaThread()
    {
        Exception? failure = null;
        Guid iconId = Guid.NewGuid();
        Thread thread = new(() =>
        {
            try
            {
                RenderBarcodeCommand barcode = new()
                {
                    SourceId = Guid.NewGuid(),
                    LocalBounds = new MmRect(5, 5, 30, 30),
                    Content = "RMPP QR ICON",
                    Symbology = BarcodeSymbology.QrCode,
                    QuietZoneMm = 2,
                    ErrorCorrectionLevel = 3,
                    ShowHumanReadableText = false,
                    HumanReadableTextStyle = new RenderTextStyle { FontFamily = "Microsoft YaHei", FontSizePoints = 8 },
                    CenterIcon = new RenderImage { AssetId = iconId, FitMode = ImageFitMode.Contain },
                    CenterIconScale = 0.2,
                };
                RenderPage page = new()
                {
                    PageNumber = 1,
                    Size = new MmSize(40, 40),
                    Clip = RenderClip.FromRectangle(new MmRect(0, 0, 40, 40)),
                    Commands = [barcode],
                };

                _ = new WpfPrintSceneRenderer().Render(page, assetProvider: new SolidIconProvider(iconId));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    private sealed class SolidIconProvider(Guid assetId) : IRenderAssetProvider
    {
        public DecodedImage? Load(RenderImage image, double targetDpi)
        {
            if (image.AssetId != assetId)
            {
                return null;
            }

            SKBitmap bitmap = new(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul));
            bitmap.Erase(SKColors.CornflowerBlue);
            return new DecodedImage(bitmap);
        }
    }
}
