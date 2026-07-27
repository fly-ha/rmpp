using System.Windows;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Printing.Windows.Rendering;
using Rmpp.Rendering.Scene;
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
}
