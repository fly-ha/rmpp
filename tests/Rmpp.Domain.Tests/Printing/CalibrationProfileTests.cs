using Rmpp.Domain.Printing;
using Xunit;

namespace Rmpp.Domain.Tests.Printing;

public sealed class CalibrationProfileTests
{
    [Fact]
    public void ExcessiveScaleCorrectionIsRejected()
    {
        CalibrationProfile profile = new()
        {
            Key = new PrinterMediaKey("printer", "A4"),
            ScaleX = 1.2,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Validate());
    }
}
