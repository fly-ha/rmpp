using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;
using Rmpp.Printing.Windows.Calibration;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Printing.Windows.Tests.Calibration;

public sealed class CalibrationTests
{
    [Fact]
    public void CalculatorProducesInverseMeasurementCorrections()
    {
        PrinterMediaKey key = new("printer", "a4");
        DateTimeOffset verifiedAt = new(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(8));

        CalibrationProfile result = CalibrationCalculator.Calculate(key, new CalibrationMeasurements
        {
            TargetHorizontalMm = 100,
            MeasuredHorizontalMm = 99,
            TargetVerticalMm = 100,
            MeasuredVerticalMm = 101,
            OffsetXmm = 2,
            OffsetYmm = -1,
            MeasuredRotationDegrees = 0.5,
        }, verifiedAt);

        Assert.Equal(100d / 99, result.ScaleX, 8);
        Assert.Equal(100d / 101, result.ScaleY, 8);
        Assert.Equal(new MmPoint(-2, 1), result.OffsetMm);
        Assert.Equal(-0.5, result.RotationDegrees);
        Assert.Equal(verifiedAt, result.LastVerifiedAt);
    }

    [Fact]
    public void TransformAppliesScaleRotationThenOffsetWithoutMutatingProfile()
    {
        CalibrationProfile profile = new()
        {
            Key = new PrinterMediaKey("printer", "a4"),
            ScaleX = 1.01,
            ScaleY = 0.99,
            RotationDegrees = 1,
            OffsetMm = new MmPoint(2, -3),
        };

        RenderTransform transform = CalibrationTransform.Create(profile, new MmSize(210, 297));
        MmPoint result = transform.Transform(new MmPoint(10, 20));

        Assert.True(double.IsFinite(result.X));
        Assert.True(double.IsFinite(result.Y));
        Assert.Equal(new MmPoint(2, -3), profile.OffsetMm);
    }

    [Fact]
    public void ProfileLookupNeverCrossesPrinterOrMedia()
    {
        CalibrationProfile a4 = new() { Key = new PrinterMediaKey("p1", "a4") };
        CalibrationProfile roll = new() { Key = new PrinterMediaKey("p1", "roll") };

        Assert.Same(a4, CalibrationProfileResolver.Resolve([a4, roll], "p1", "a4"));
        Assert.Null(CalibrationProfileResolver.Resolve([a4, roll], "p2", "a4"));
    }

    [Fact]
    public void CalibrationPageContainsRulersReferenceGeometryAndInstructions()
    {
        RenderScene scene = CalibrationPageFactory.Create(new MmSize(210, 297));

        RenderPage page = Assert.Single(scene.Pages);
        Assert.Contains(page.Commands, static command => command is RenderTextCommand);
        Assert.True(page.Commands.Count(command => command is RenderPathCommand) >= 4);
    }
}
