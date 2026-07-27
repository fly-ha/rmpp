using Rmpp.Domain.Styles;
using Rmpp.Rendering.Fills;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Rendering.Tests.Fills;

public sealed class HatchPatternFactoryTests
{
    [Theory]
    [InlineData(HatchPattern.Horizontal, 1)]
    [InlineData(HatchPattern.Vertical, 1)]
    [InlineData(HatchPattern.Cross, 2)]
    [InlineData(HatchPattern.DiagonalCross, 2)]
    [InlineData(HatchPattern.Grid, 2)]
    public void LinePatternsProduceStableTileGeometry(HatchPattern pattern, int primitiveCount)
    {
        RenderHatchFill fill = HatchPatternFactory.Create(new HatchFill
        {
            Pattern = pattern,
            SpacingMm = 2.5,
            AngleDegrees = -30,
            LineWidthMm = 0.2,
        });

        Assert.Equal(2.5, fill.Tile.Size.Width);
        Assert.Equal(2.5, fill.Tile.Size.Height);
        Assert.Equal(330, fill.Tile.RotationDegrees);
        Assert.Equal(primitiveCount, fill.Tile.Primitives.Count);
    }

    [Fact]
    public void DotPatternUsesLineWidthAsMinimumDotDiameter()
    {
        RenderHatchFill fill = HatchPatternFactory.Create(new HatchFill
        {
            Pattern = HatchPattern.Dots,
            SpacingMm = 3,
            LineWidthMm = 0.4,
        });

        HatchDot dot = Assert.IsType<HatchDot>(Assert.Single(fill.Tile.Primitives));
        Assert.Equal(0.2, dot.RadiusMm);
    }
}
