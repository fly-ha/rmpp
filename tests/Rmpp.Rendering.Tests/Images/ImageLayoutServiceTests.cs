using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Images;
using Xunit;

namespace Rmpp.Rendering.Tests.Images;

public sealed class ImageLayoutServiceTests
{
    [Fact]
    public void ContainCentersWithoutCropping()
    {
        ImageLayoutResult result = ImageLayoutService.Layout(400, 200, new MmRect(0, 0, 40, 40), ImageFitMode.Contain);

        Assert.Equal(new MmRect(0, 0, 400, 200), result.SourcePixels);
        Assert.Equal(new MmRect(0, 10, 40, 20), result.DestinationMm);
    }

    [Fact]
    public void CoverCropsSourceToTargetRatio()
    {
        ImageLayoutResult result = ImageLayoutService.Layout(400, 200, new MmRect(0, 0, 20, 20), ImageFitMode.Cover);

        Assert.Equal(new MmRect(100, 0, 200, 200), result.SourcePixels);
        Assert.Equal(new MmRect(0, 0, 20, 20), result.DestinationMm);
    }

    [Fact]
    public void OriginalSizeUsesExplicitSourceDpi()
    {
        ImageLayoutResult result = ImageLayoutService.Layout(300, 150, new MmRect(5, 6, 100, 100), ImageFitMode.OriginalSize, sourceDpi: 300);

        Assert.Equal(25.4, result.DestinationMm.Width, 6);
        Assert.Equal(12.7, result.DestinationMm.Height, 6);
        Assert.Equal(new MmPoint(5, 6), result.DestinationMm.Location);
    }
}
