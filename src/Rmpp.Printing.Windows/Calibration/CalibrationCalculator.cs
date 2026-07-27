using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;

namespace Rmpp.Printing.Windows.Calibration;

/// <summary>用户在纸面量得的已知距离和偏移，用于计算小范围设备校正。</summary>
public sealed record CalibrationMeasurements
{
    public double TargetHorizontalMm { get; init; } = 100;
    public double MeasuredHorizontalMm { get; init; } = 100;
    public double TargetVerticalMm { get; init; } = 100;
    public double MeasuredVerticalMm { get; init; } = 100;
    public double OffsetXmm { get; init; }
    public double OffsetYmm { get; init; }
    public double MeasuredRotationDegrees { get; init; }
}

/// <summary>按目标值/实测值计算比例，并用反向角度抵消纸面旋转误差。</summary>
public sealed class CalibrationCalculator
{
    public static CalibrationProfile Calculate(
        PrinterMediaKey key,
        CalibrationMeasurements measurements,
        DateTimeOffset verifiedAt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(measurements);
        ValidateDistance(measurements.TargetHorizontalMm, nameof(measurements.TargetHorizontalMm));
        ValidateDistance(measurements.MeasuredHorizontalMm, nameof(measurements.MeasuredHorizontalMm));
        ValidateDistance(measurements.TargetVerticalMm, nameof(measurements.TargetVerticalMm));
        ValidateDistance(measurements.MeasuredVerticalMm, nameof(measurements.MeasuredVerticalMm));

        return new CalibrationProfile
        {
            Key = key,
            OffsetMm = new MmPoint(-measurements.OffsetXmm, -measurements.OffsetYmm),
            ScaleX = measurements.TargetHorizontalMm / measurements.MeasuredHorizontalMm,
            ScaleY = measurements.TargetVerticalMm / measurements.MeasuredVerticalMm,
            RotationDegrees = -measurements.MeasuredRotationDegrees,
            LastVerifiedAt = verifiedAt,
        }.Validate();
    }

    private static void ValidateDistance(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "校准距离必须是大于零的有限值。");
        }
    }
}
