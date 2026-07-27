using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Elements;

public abstract record PathElement : ShapeElement
{
    public IReadOnlyList<MmPoint> Points { get; init; } = Array.Empty<MmPoint>();
}
