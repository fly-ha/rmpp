using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rmpp.Domain.Geometry;
using Rmpp.Printing.Windows.Printers;

namespace Rmpp.Printing.Windows.Rendering;

/// <summary>把无法安全转成 XPS 矢量的区域按受控 DPI 栅格化，并限制中间像素总量。</summary>
public sealed class RasterFallbackRenderer
{
    public const long DefaultMaximumPixels = 100_000_000;

    public static BitmapSource Render(
        DrawingVisual visual,
        MmSize physicalSize,
        int dpi,
        long maximumPixels = DefaultMaximumPixels)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (dpi is < 96 or > 2400)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), "栅格回退 DPI 必须在 96–2400 之间。");
        }

        int width = checked((int)Math.Ceiling(physicalSize.Width / 25.4 * dpi));
        int height = checked((int)Math.Ceiling(physicalSize.Height / 25.4 * dpi));
        long pixels = checked((long)width * height);
        if (pixels <= 0 || pixels > maximumPixels)
        {
            throw new InvalidOperationException($"栅格回退需要 {pixels:N0} 像素，超过限制 {maximumPixels:N0}。");
        }

        RenderTargetBitmap target = new(width, height, dpi, dpi, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }
}
