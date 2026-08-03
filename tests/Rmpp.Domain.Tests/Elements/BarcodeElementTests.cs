using Rmpp.Domain.Elements;
using Xunit;

namespace Rmpp.Domain.Tests.Elements;

public sealed class BarcodeElementTests
{
    [Theory]
    [InlineData(0.09)]
    [InlineData(0.26)]
    [InlineData(double.NaN)]
    public void CenterIconScaleOutsideSafeRangeIsRejected(double scale)
    {
        BarcodeElement barcode = new()
        {
            Symbology = BarcodeSymbology.QrCode,
            CenterIconAssetId = Guid.NewGuid(),
            CenterIconScale = scale,
        };

        Assert.Throws<InvalidOperationException>(() => barcode.Validate());
    }

    [Fact]
    public void NonQrBarcodeCannotRetainCenterIcon()
    {
        BarcodeElement barcode = new()
        {
            Symbology = BarcodeSymbology.Code128,
            CenterIconAssetId = Guid.NewGuid(),
        };

        Assert.Throws<InvalidOperationException>(() => barcode.Validate());
    }
}
