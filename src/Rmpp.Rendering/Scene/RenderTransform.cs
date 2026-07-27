using Rmpp.Domain.Geometry;

namespace Rmpp.Rendering.Scene;

/// <summary>表示毫米空间中的二维仿射变换；Then按当前变换后接下一变换组合。</summary>
public readonly record struct RenderTransform(
    double M11,
    double M12,
    double M21,
    double M22,
    double OffsetX,
    double OffsetY)
{
    public static RenderTransform Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public static RenderTransform Translation(double x, double y) => new(1, 0, 0, 1, x, y);

    public static RenderTransform Scale(double x, double y) => new(x, 0, 0, y, 0, 0);

    public static RenderTransform Rotation(double degrees)
    {
        double radians = degrees * Math.PI / 180;
        double cosine = Math.Cos(radians);
        double sine = Math.Sin(radians);
        return new RenderTransform(cosine, sine, -sine, cosine, 0, 0);
    }

    public static RenderTransform RotationAt(double degrees, MmPoint center) =>
        Translation(-center.X, -center.Y)
            .Then(Rotation(degrees))
            .Then(Translation(center.X, center.Y));

    public static RenderTransform ForElement(MmRect bounds, Angle rotation) =>
        RotationAt(rotation.Degrees, new MmPoint(bounds.Width / 2, bounds.Height / 2))
            .Then(Translation(bounds.X, bounds.Y));

    public MmPoint Transform(MmPoint point) =>
        new(
            M11 * point.X + M21 * point.Y + OffsetX,
            M12 * point.X + M22 * point.Y + OffsetY);

    public RenderTransform Then(RenderTransform next) =>
        new(
            next.M11 * M11 + next.M21 * M12,
            next.M12 * M11 + next.M22 * M12,
            next.M11 * M21 + next.M21 * M22,
            next.M12 * M21 + next.M22 * M22,
            next.M11 * OffsetX + next.M21 * OffsetY + next.OffsetX,
            next.M12 * OffsetX + next.M22 * OffsetY + next.OffsetY);
}
