using Rmpp.Desktop.Utilities;
using Rmpp.Domain.Styles;
using Xunit;

namespace Rmpp.Desktop.Tests.Utilities;

public sealed class HsvColorConverterTests
{
    [Theory]
    [InlineData(255, 0, 0, 0d, 1d, 1d)]
    [InlineData(0, 255, 0, 120d, 1d, 1d)]
    [InlineData(0, 0, 255, 240d, 1d, 1d)]
    [InlineData(128, 128, 128, 0d, 0d, 128d / 255d)]
    public void FromRgbaReturnsExpectedPrimaryAndGreyValues(
        byte red,
        byte green,
        byte blue,
        double hue,
        double saturation,
        double value)
    {
        HsvColor actual = HsvColorConverter.FromRgba(new RgbaColor(red, green, blue, 17));

        Assert.Equal(hue, actual.Hue, 6);
        Assert.Equal(saturation, actual.Saturation, 6);
        Assert.Equal(value, actual.Value, 6);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(255, 128, 0)]
    [InlineData(12, 93, 201)]
    [InlineData(41, 42, 43)]
    public void RgbHsvRoundTripPreservesEightBitChannels(byte red, byte green, byte blue)
    {
        RgbaColor source = new(red, green, blue, 73);

        RgbaColor actual = HsvColorConverter.ToRgba(HsvColorConverter.FromRgba(source), source.Alpha);

        Assert.InRange(Math.Abs(actual.Red - source.Red), 0, 1);
        Assert.InRange(Math.Abs(actual.Green - source.Green), 0, 1);
        Assert.InRange(Math.Abs(actual.Blue - source.Blue), 0, 1);
        Assert.Equal(source.Alpha, actual.Alpha);
    }

    [Fact]
    public void ToRgbaClampsRangesAndWrapsHue()
    {
        RgbaColor actual = HsvColorConverter.ToRgba(new HsvColor(420d, 2d, 2d), 11);

        Assert.Equal(new RgbaColor(255, 255, 0, 11), actual);
    }

    [Theory]
    [InlineData("0", "1", "254", "255", true)]
    [InlineData("", "1", "2", "3", false)]
    [InlineData("-1", "1", "2", "3", false)]
    [InlineData("256", "1", "2", "3", false)]
    [InlineData("1.0", "1", "2", "3", false)]
    public void ChannelParsingOnlyAcceptsCompleteByteValues(
        string red,
        string green,
        string blue,
        string alpha,
        bool expected)
    {
        bool actual = RgbaColorText.TryParseChannels(red, green, blue, alpha, out RgbaColor color);

        Assert.Equal(expected, actual);
        if (expected)
        {
            Assert.Equal(new RgbaColor(0, 1, 254, 255), color);
        }
    }
}
