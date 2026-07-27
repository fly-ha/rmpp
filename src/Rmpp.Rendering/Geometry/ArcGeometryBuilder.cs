using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Geometry;

/// <summary>把椭圆弧按最多90度的确定性分段转换为三次贝塞尔路径。</summary>
public static class ArcGeometryBuilder
{
    public static RenderPath BuildArc(MmRect bounds, Angle startAngle, double sweepDegrees)
    {
        ValidateArc(bounds, sweepDegrees);
        RenderPathBuilder builder = new();
        MmPoint start = PointAt(bounds, startAngle.Degrees);
        builder.MoveTo(start);
        AppendArc(builder, bounds, startAngle.Degrees, sweepDegrees);
        return builder.Build();
    }

    public static RenderPath BuildSector(MmRect bounds, Angle startAngle, double sweepDegrees)
    {
        ValidateArc(bounds, sweepDegrees);
        MmPoint center = new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        MmPoint start = PointAt(bounds, startAngle.Degrees);
        RenderPathBuilder builder = new();
        builder.MoveTo(center).LineTo(start);
        AppendArc(builder, bounds, startAngle.Degrees, sweepDegrees);
        return builder.Close().Build();
    }

    public static void AppendArc(
        RenderPathBuilder builder,
        MmRect bounds,
        double startDegrees,
        double sweepDegrees)
    {
        ArgumentNullException.ThrowIfNull(builder);
        int segmentCount = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepDegrees) / 90));
        double segmentSweep = sweepDegrees / segmentCount;
        double current = startDegrees;
        for (int index = 0; index < segmentCount; index++)
        {
            AppendSegment(builder, bounds, current, segmentSweep);
            current += segmentSweep;
        }
    }

    public static MmPoint PointAt(MmRect bounds, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        return new MmPoint(
            bounds.X + bounds.Width / 2 + bounds.Width / 2 * Math.Cos(radians),
            bounds.Y + bounds.Height / 2 + bounds.Height / 2 * Math.Sin(radians));
    }

    private static void AppendSegment(RenderPathBuilder builder, MmRect bounds, double startDegrees, double sweepDegrees)
    {
        double start = startDegrees * Math.PI / 180;
        double end = (startDegrees + sweepDegrees) * Math.PI / 180;
        double radiusX = bounds.Width / 2;
        double radiusY = bounds.Height / 2;
        double centerX = bounds.X + radiusX;
        double centerY = bounds.Y + radiusY;
        double factor = 4d / 3d * Math.Tan((end - start) / 4d);

        MmPoint startPoint = new(centerX + radiusX * Math.Cos(start), centerY + radiusY * Math.Sin(start));
        MmPoint endPoint = new(centerX + radiusX * Math.Cos(end), centerY + radiusY * Math.Sin(end));
        MmPoint startDerivative = new(-radiusX * Math.Sin(start), radiusY * Math.Cos(start));
        MmPoint endDerivative = new(-radiusX * Math.Sin(end), radiusY * Math.Cos(end));
        builder.CubicTo(
            new MmPoint(startPoint.X + factor * startDerivative.X, startPoint.Y + factor * startDerivative.Y),
            new MmPoint(endPoint.X - factor * endDerivative.X, endPoint.Y - factor * endDerivative.Y),
            endPoint);
    }

    private static void ValidateArc(MmRect bounds, double sweepDegrees)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentException("Arc bounds must have positive size.", nameof(bounds));
        }

        if (!double.IsFinite(sweepDegrees) || sweepDegrees == 0 || Math.Abs(sweepDegrees) > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(sweepDegrees), "Arc sweep must be finite, non-zero, and at most 360 degrees.");
        }
    }
}
