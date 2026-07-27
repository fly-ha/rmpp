using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Elements;

public sealed record RectangleElement : ShapeElement
{
    public MmSize CornerRadius { get; init; }
}
