using Rmpp.Domain.Styles;

namespace Rmpp.Desktop.Utilities;

/// <summary>表示调色器使用的 HSV 连续颜色，其中色相为角度，饱和度与明度范围为 0 至 1。</summary>
internal readonly record struct HsvColor(double Hue, double Saturation, double Value);

/// <summary>在领域层的 8 位 RGBA 与桌面调色器的 HSV 连续空间之间执行确定性转换。</summary>
internal static class HsvColorConverter
{
    public static HsvColor Normalize(HsvColor hsv) =>
        new(NormalizeHue(hsv.Hue), Math.Clamp(hsv.Saturation, 0d, 1d), Math.Clamp(hsv.Value, 0d, 1d));

    public static HsvColor FromRgba(RgbaColor color)
    {
        double red = color.Red / 255d;
        double green = color.Green / 255d;
        double blue = color.Blue / 255d;
        double maximum = Math.Max(red, Math.Max(green, blue));
        double minimum = Math.Min(red, Math.Min(green, blue));
        double delta = maximum - minimum;

        double hue = 0d;
        if (delta > double.Epsilon)
        {
            if (Math.Abs(maximum - red) < double.Epsilon)
            {
                hue = 60d * (((green - blue) / delta) % 6d);
            }
            else if (Math.Abs(maximum - green) < double.Epsilon)
            {
                hue = 60d * (((blue - red) / delta) + 2d);
            }
            else
            {
                hue = 60d * (((red - green) / delta) + 4d);
            }
        }

        if (hue < 0d)
        {
            hue += 360d;
        }

        double saturation = maximum <= double.Epsilon ? 0d : delta / maximum;
        return new HsvColor(hue, saturation, maximum);
    }

    public static RgbaColor ToRgba(HsvColor hsv, byte alpha = byte.MaxValue)
    {
        HsvColor normalized = Normalize(hsv);
        double hue = normalized.Hue;
        double saturation = normalized.Saturation;
        double value = normalized.Value;
        double chroma = value * saturation;
        double hueSection = hue / 60d;
        double secondary = chroma * (1d - Math.Abs((hueSection % 2d) - 1d));

        (double red, double green, double blue) = hueSection switch
        {
            < 1d => (chroma, secondary, 0d),
            < 2d => (secondary, chroma, 0d),
            < 3d => (0d, chroma, secondary),
            < 4d => (0d, secondary, chroma),
            < 5d => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary),
        };

        double match = value - chroma;
        return new RgbaColor(
            ToByte(red + match),
            ToByte(green + match),
            ToByte(blue + match),
            alpha);
    }

    private static double NormalizeHue(double hue)
    {
        if (!double.IsFinite(hue))
        {
            return 0d;
        }

        double normalized = hue % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    private static byte ToByte(double value) =>
        checked((byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d, MidpointRounding.AwayFromZero));
}
