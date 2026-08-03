using System.Globalization;
using Rmpp.Domain.Styles;

namespace Rmpp.Desktop.Utilities;

/// <summary>统一桌面属性面板的 RGB/RGBA 文本格式，避免不同属性各自解释透明度。</summary>
internal static class RgbaColorText
{
    public static string Format(RgbaColor color) =>
        $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    public static bool TryParse(string? value, out RgbaColor color)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length == 6
            && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            color = new RgbaColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }

        if (text.Length == 8
            && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
        {
            color = new RgbaColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
            return true;
        }

        color = default;
        return false;
    }
}
