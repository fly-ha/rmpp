using Rmpp.Domain.Common;

namespace Rmpp.Domain.Styles;

public enum TextHorizontalAlignment { Left, Center, Right, Justify }
public enum TextVerticalAlignment { Top, Center, Bottom }
public enum TextOverflowMode { Warn, Clip, AutoFit, ExpandHeight }

public sealed record TextStyle
{
    public string FontFamily { get; init; } = "Microsoft YaHei";
    public double FontSizePoints { get; init; } = 10;
    public bool IsBold { get; init; }
    public bool IsItalic { get; init; }
    public bool IsUnderline { get; init; }
    public RgbaColor Color { get; init; } = RgbaColor.Black;
    public TextHorizontalAlignment HorizontalAlignment { get; init; }
    public TextVerticalAlignment VerticalAlignment { get; init; }
    public bool Wrap { get; init; } = true;
    public double LineSpacing { get; init; } = 1;
    public double LetterSpacingMm { get; init; }
    public TextOverflowMode OverflowMode { get; init; } = TextOverflowMode.Warn;

    public TextStyle Validate()
    {
        DomainGuard.Required(FontFamily, nameof(FontFamily));
        if (DomainGuard.NonNegative(FontSizePoints, nameof(FontSizePoints)) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FontSizePoints), "Font size must be greater than zero.");
        }

        if (DomainGuard.NonNegative(LineSpacing, nameof(LineSpacing)) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LineSpacing), "Line spacing must be greater than zero.");
        }

        DomainGuard.Finite(LetterSpacingMm, nameof(LetterSpacingMm));
        return this;
    }
}
