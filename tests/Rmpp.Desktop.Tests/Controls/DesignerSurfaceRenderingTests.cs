using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using Rmpp.Desktop.Controls;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Xunit;

namespace Rmpp.Desktop.Tests.Controls;

public sealed class DesignerSurfaceRenderingTests
{
    [Fact]
    public void LandscapeMediaSwapsTheDesignerCanvasDimensionsOnSta()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                TemplateDocument document = TemplateDocument.CreateNew("横向画布") with
                {
                    Page = TemplateDocument.CreateNew("横向画布").Page with
                    {
                        Media = new MediaDefinition("A4", new MmSize(210, 297), PageOrientation.Landscape),
                    },
                };
                DesignerSurface surface = new() { Document = document, Zoom = 1 };

                surface.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                const double dipPerMm = 96d / 25.4;
                Assert.Equal(297 * dipPerMm + 64, surface.DesiredSize.Width, 3);
                Assert.Equal(210 * dipPerMm + 64, surface.DesiredSize.Height, 3);
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

    [Fact]
    public void EllipseUsesTheSharedShapeRendererInsteadOfAGenericRectangleOnSta()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                TemplateDocument document = TemplateDocument.CreateNew("真实预览");
                EllipseElement ellipse = new()
                {
                    LayerId = document.Layers[0].Id,
                    Bounds = new MmRect(20, 20, 30, 20),
                };
                document = document with { Elements = [ellipse] };
                DesignerSurface surface = new() { Document = document, ShowGrid = false, Zoom = 1 };
                MethodInfo getElementLayer = typeof(DesignerSurface).GetMethod(
                    "GetElementLayer",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("找不到编辑器元素图层方法。");
                BitmapSource bitmap = Assert.IsAssignableFrom<BitmapSource>(getElementLayer.Invoke(surface, null));
                const double dipPerMm = 96d / 25.4;
                int left = (int)Math.Round(ellipse.Bounds.X * dipPerMm);
                int top = (int)Math.Round(ellipse.Bounds.Y * dipPerMm);
                int centerX = (int)Math.Round((ellipse.Bounds.X + ellipse.Bounds.Width / 2) * dipPerMm);

                Color corner = GetPixel(bitmap, left, top);
                Assert.Equal(0, corner.A);
                Assert.Contains(
                    Enumerable.Range(-2, 5).Select(offset => GetPixel(bitmap, centerX + offset, top)),
                    color => color.A > 0);
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

    private static RenderTargetBitmap Render(TemplateDocument document)
    {
        DesignerSurface surface = new()
        {
            Document = document,
            ShowGrid = false,
            Zoom = 1,
        };
        surface.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        surface.Arrange(new Rect(surface.DesiredSize));
        RenderTargetBitmap bitmap = new(
            Math.Max(1, (int)Math.Ceiling(surface.ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(surface.ActualHeight)),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(surface);
        return bitmap;
    }

    private static Color GetPixel(BitmapSource bitmap, int x, int y)
    {
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }
}
