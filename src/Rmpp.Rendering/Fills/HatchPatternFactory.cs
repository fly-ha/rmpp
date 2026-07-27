using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Fills;

/// <summary>把领域填充转换为具有固定毫米间距和可重复tile的后端无关填充。</summary>
public static class HatchPatternFactory
{
    public static RenderFill CreateFill(FillStyle fill) => fill switch
    {
        NoFill => new RenderNoFill(),
        SolidFill solid => new RenderSolidFill(solid.Color),
        HatchFill hatch => Create(hatch),
        _ => throw new NotSupportedException($"Unsupported fill style: {fill.GetType().Name}"),
    };

    public static RenderHatchFill Create(HatchFill fill)
    {
        HatchFill validated = fill.Validate();
        double spacing = validated.SpacingMm;
        double center = spacing / 2;
        List<HatchPrimitive> primitives = validated.Pattern switch
        {
            HatchPattern.Horizontal => [new HatchLine(new MmPoint(0, center), new MmPoint(spacing, center))],
            HatchPattern.Vertical => [new HatchLine(new MmPoint(center, 0), new MmPoint(center, spacing))],
            HatchPattern.ForwardDiagonal => [new HatchLine(new MmPoint(0, spacing), new MmPoint(spacing, 0))],
            HatchPattern.BackwardDiagonal => [new HatchLine(new MmPoint(0, 0), new MmPoint(spacing, spacing))],
            HatchPattern.Cross =>
            [
                new HatchLine(new MmPoint(0, center), new MmPoint(spacing, center)),
                new HatchLine(new MmPoint(center, 0), new MmPoint(center, spacing)),
            ],
            HatchPattern.DiagonalCross =>
            [
                new HatchLine(new MmPoint(0, 0), new MmPoint(spacing, spacing)),
                new HatchLine(new MmPoint(0, spacing), new MmPoint(spacing, 0)),
            ],
            HatchPattern.Dots => [new HatchDot(new MmPoint(center, center), Math.Max(validated.LineWidthMm / 2, 0.01))],
            HatchPattern.Grid =>
            [
                new HatchLine(new MmPoint(0, 0), new MmPoint(spacing, 0)),
                new HatchLine(new MmPoint(0, 0), new MmPoint(0, spacing)),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(fill), "Unknown hatch pattern."),
        };

        return new RenderHatchFill(
            validated.Foreground,
            validated.Background,
            validated.Pattern,
            new HatchTile(
                new MmSize(spacing, spacing),
                NormalizeAngle(validated.AngleDegrees),
                validated.LineWidthMm,
                primitives.ToArray()));
    }

    private static double NormalizeAngle(double degrees) => ((degrees % 360) + 360) % 360;
}
