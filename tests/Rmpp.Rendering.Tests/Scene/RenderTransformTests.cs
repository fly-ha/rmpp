using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Rendering.Tests.Scene;

public sealed class RenderTransformTests
{
    [Fact]
    public void ElementTransformRotatesAroundLocalCenterThenTranslates()
    {
        MmRect bounds = new(10, 20, 10, 20);
        RenderTransform transform = RenderTransform.ForElement(bounds, new Angle(90));

        MmPoint result = transform.Transform(new MmPoint(0, 0));

        Assert.Equal(25, result.X, precision: 10);
        Assert.Equal(25, result.Y, precision: 10);
    }

    [Fact]
    public void ThenAppliesTransformsInDeclaredOrder()
    {
        RenderTransform transform = RenderTransform.Scale(2, 3).Then(RenderTransform.Translation(5, 7));

        MmPoint result = transform.Transform(new MmPoint(4, 6));

        Assert.Equal(new MmPoint(13, 25), result);
    }
}
