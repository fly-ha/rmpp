using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Xunit;

namespace Rmpp.Domain.Tests.Elements;

public sealed class ElementInvariantTests
{
    [Fact]
    public void PolygonRequiresThreeDistinctPoints()
    {
        PolygonElement polygon = new()
        {
            Name = "Invalid polygon",
            Points = [new MmPoint(0, 0), new MmPoint(1, 1), new MmPoint(0, 0)],
        };

        Assert.Throws<InvalidOperationException>(() => polygon.Validate());
    }

    [Fact]
    public void HatchRequiresPositiveSpacing()
    {
        HatchFill fill = new() { SpacingMm = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => fill.Validate());
    }

    [Fact]
    public void ValidPolylinePassesValidation()
    {
        PolylineElement polyline = new()
        {
            Name = "Line",
            Points = [new MmPoint(0, 0), new MmPoint(1, 1)],
        };

        Assert.Same(polyline, polyline.Validate());
    }
}
