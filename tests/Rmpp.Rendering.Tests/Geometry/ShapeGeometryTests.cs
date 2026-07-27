using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Geometry;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Rendering.Tests.Geometry;

public sealed class ShapeGeometryTests
{
    [Fact]
    public void FullEllipseUsesFourCubicSegmentsAndCloses()
    {
        EllipseElement ellipse = new() { Bounds = new MmRect(0, 0, 40, 20) };

        RenderPath path = ShapePathFactory.Create(ellipse);

        Assert.Equal(6, path.Segments.Count);
        Assert.Equal(4, path.Segments.OfType<RenderCubicTo>().Count());
        Assert.IsType<RenderClosePath>(path.Segments[^1]);
    }

    [Fact]
    public void ArcIsSplitIntoSegmentsNoLargerThanNinetyDegrees()
    {
        RenderPath path = ArcGeometryBuilder.BuildArc(new MmRect(0, 0, 20, 10), Angle.Zero, 200);

        Assert.Equal(3, path.Segments.OfType<RenderCubicTo>().Count());
        Assert.DoesNotContain(path.Segments, static segment => segment is RenderClosePath);
    }

    [Fact]
    public void SectorStartsAtCenterAndCloses()
    {
        RenderPath path = ArcGeometryBuilder.BuildSector(new MmRect(0, 0, 20, 10), Angle.Zero, 90);

        Assert.Equal(new MmPoint(10, 5), Assert.IsType<RenderMoveTo>(path.Segments[0]).Point);
        Assert.IsType<RenderClosePath>(path.Segments[^1]);
    }

    [Fact]
    public void PolygonWindingIsNormalizedAndDegeneratePolygonIsRejected()
    {
        MmPoint[] clockwiseOrCounterClockwise =
        [
            new MmPoint(0, 0),
            new MmPoint(0, 10),
            new MmPoint(10, 0),
        ];

        MmPoint[] normalized = PolygonGeometryBuilder.NormalizeWinding(clockwiseOrCounterClockwise);

        Assert.True(PolygonGeometryBuilder.SignedArea(normalized) > 0);
        Assert.Throws<InvalidOperationException>(() => PolygonGeometryBuilder.Build(
            [new MmPoint(0, 0), new MmPoint(5, 0), new MmPoint(10, 0)]));
    }

    [Fact]
    public void RoundedRectangleClampsCornerRadiusAndUsesCubics()
    {
        RectangleElement rectangle = new()
        {
            Bounds = new MmRect(0, 0, 20, 10),
            CornerRadius = new MmSize(50, 50),
        };

        RenderPath path = ShapePathFactory.Create(rectangle);

        Assert.Equal(4, path.Segments.OfType<RenderCubicTo>().Count());
        Assert.IsType<RenderClosePath>(path.Segments[^1]);
    }

    [Fact]
    public void PolylineDropsConsecutiveDuplicatePointsButRemainsOpen()
    {
        RenderPath path = PolylineGeometryBuilder.Build(
        [
            new MmPoint(0, 0),
            new MmPoint(0, 0),
            new MmPoint(10, 10),
        ]);

        Assert.Equal(2, path.Segments.Count);
        Assert.DoesNotContain(path.Segments, static segment => segment is RenderClosePath);
    }
}
