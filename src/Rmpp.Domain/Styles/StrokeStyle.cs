using Rmpp.Domain.Common;

namespace Rmpp.Domain.Styles;

public enum StrokeLineCap { Flat, Square, Round }

public enum StrokeLineJoin { Miter, Bevel, Round }

public sealed record StrokeStyle
{
    public bool IsEnabled { get; init; } = true;
    public RgbaColor Color { get; init; } = RgbaColor.Black;
    public double WidthMm { get; init; } = 0.2;
    public DashPattern DashPattern { get; init; } = DashPattern.Solid;
    public StrokeLineCap LineCap { get; init; } = StrokeLineCap.Flat;
    public StrokeLineJoin LineJoin { get; init; } = StrokeLineJoin.Miter;

    public StrokeStyle Validate()
    {
        DomainGuard.NonNegative(WidthMm, nameof(WidthMm));
        return this;
    }

    public static StrokeStyle None { get; } = new() { IsEnabled = false, WidthMm = 0 };
}
