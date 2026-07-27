using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;

namespace Rmpp.Rendering.Scene;

public sealed record RenderTextStyle
{
    public required string FontFamily { get; init; }
    public double FontSizePoints { get; init; }
    public bool IsBold { get; init; }
    public bool IsItalic { get; init; }
    public bool IsUnderline { get; init; }
    public RgbaColor Color { get; init; }
    public TextHorizontalAlignment HorizontalAlignment { get; init; }
    public TextVerticalAlignment VerticalAlignment { get; init; }
    public bool Wrap { get; init; }
    public double LineSpacing { get; init; }
    public double LetterSpacingMm { get; init; }
    public TextOverflowMode OverflowMode { get; init; }

    public static RenderTextStyle FromDomain(TextStyle style)
    {
        TextStyle validated = style.Validate();
        return new RenderTextStyle
        {
            FontFamily = validated.FontFamily,
            FontSizePoints = validated.FontSizePoints,
            IsBold = validated.IsBold,
            IsItalic = validated.IsItalic,
            IsUnderline = validated.IsUnderline,
            Color = validated.Color,
            HorizontalAlignment = validated.HorizontalAlignment,
            VerticalAlignment = validated.VerticalAlignment,
            Wrap = validated.Wrap,
            LineSpacing = validated.LineSpacing,
            LetterSpacingMm = validated.LetterSpacingMm,
            OverflowMode = validated.OverflowMode,
        };
    }
}

public sealed record RenderGlyph(int GlyphId, MmPoint Origin, double AdvanceMm);

/// <summary>表示已完成字体解析和排版的字形序列，供Phase 6文本服务填充。</summary>
public sealed record RenderGlyphRun(
    string FontFace,
    double FontSizePoints,
    RgbaColor Color,
    IReadOnlyList<RenderGlyph> Glyphs,
    MmRect Bounds);
