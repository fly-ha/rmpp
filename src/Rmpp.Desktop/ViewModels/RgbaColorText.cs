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

    /// <summary>仅在四个通道均为完整的 0 至 255 十进制整数时生成颜色，输入中间态不会被截断或钳制。</summary>
    public static bool TryParseChannels(
        string? redText,
        string? greenText,
        string? blueText,
        string? alphaText,
        out RgbaColor color)
    {
        if (TryParseChannel(redText, out byte red)
            && TryParseChannel(greenText, out byte green)
            && TryParseChannel(blueText, out byte blue)
            && TryParseChannel(alphaText, out byte alpha))
        {
            color = new RgbaColor(red, green, blue, alpha);
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryParseChannel(string? value, out byte channel) =>
        byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out channel);
}
