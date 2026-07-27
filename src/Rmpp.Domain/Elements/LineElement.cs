using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Elements;

public sealed record LineElement : ShapeElement
{
    public MmPoint Start { get; init; }
    public MmPoint End { get; init; }
}
