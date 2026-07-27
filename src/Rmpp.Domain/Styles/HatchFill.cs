using Rmpp.Domain.Common;

namespace Rmpp.Domain.Styles;

public sealed record HatchFill : FillStyle
{
    public HatchPattern Pattern { get; init; } = HatchPattern.ForwardDiagonal;
    public RgbaColor Foreground { get; init; } = RgbaColor.Black;
    public RgbaColor Background { get; init; } = RgbaColor.Transparent;
    public double SpacingMm { get; init; } = 2;
    public double AngleDegrees { get; init; }
    public double LineWidthMm { get; init; } = 0.2;

    public HatchFill Validate()
    {
        if (DomainGuard.NonNegative(SpacingMm, nameof(SpacingMm)) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SpacingMm), "Spacing must be greater than zero.");
        }

        DomainGuard.NonNegative(LineWidthMm, nameof(LineWidthMm));
        DomainGuard.Finite(AngleDegrees, nameof(AngleDegrees));
        return this;
    }
}
