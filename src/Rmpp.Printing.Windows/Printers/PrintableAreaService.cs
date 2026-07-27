using Rmpp.Domain.Geometry;

namespace Rmpp.Printing.Windows.Printers;

/// <summary>统一 Windows 96-DPI DIP 与毫米，并把异常驱动范围限制在介质内部。</summary>
public sealed class PrintableAreaService
{
    public const double DipPerInch = 96;
    public const double MillimetresPerInch = 25.4;

    public static MmRect FromDeviceIndependentPixels(
        double originX,
        double originY,
        double extentWidth,
        double extentHeight,
        MmSize mediaSize)
    {
        double x = Math.Clamp(ToMillimetres(originX), 0, mediaSize.Width);
        double y = Math.Clamp(ToMillimetres(originY), 0, mediaSize.Height);
        double width = Math.Clamp(ToMillimetres(extentWidth), 0, mediaSize.Width - x);
        double height = Math.Clamp(ToMillimetres(extentHeight), 0, mediaSize.Height - y);
        return new MmRect(x, y, width, height);
    }

    public static double ToMillimetres(double dip) => dip * MillimetresPerInch / DipPerInch;

    public static double ToDeviceIndependentPixels(double millimetres) =>
        millimetres * DipPerInch / MillimetresPerInch;
}
