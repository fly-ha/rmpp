using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Elements;

public sealed record ArcElement : ShapeElement
{
    public Angle StartAngle { get; init; }
    public double SweepDegrees { get; init; } = 90;
}
