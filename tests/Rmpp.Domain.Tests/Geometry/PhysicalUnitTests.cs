using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Domain.Tests.Geometry;

public sealed class PhysicalUnitTests
{
    [Fact]
    public void AngleNormalizesNegativeDegrees()
    {
        Assert.Equal(350, new Angle(-10).Degrees);
    }

    [Fact]
    public void RectTranslationPreservesSize()
    {
        MmRect translated = new MmRect(1, 2, 30, 40).Translate(5, -1);

        Assert.Equal(new MmRect(6, 1, 30, 40), translated);
    }

    [Fact]
    public void NegativeSizeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MmSize(-1, 2));
    }
}
