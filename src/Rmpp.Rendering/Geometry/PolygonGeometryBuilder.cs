using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Geometry;

/// <summary>验证多边形非退化并统一为正有向面积的稳定绕向。</summary>
public static class PolygonGeometryBuilder
{
    public static RenderPath Build(IEnumerable<MmPoint> points)
    {
        MmPoint[] normalized = NormalizeWinding(points);
        RenderPathBuilder builder = new();
        builder.MoveTo(normalized[0]);
        foreach (MmPoint point in normalized.Skip(1))
        {
            builder.LineTo(point);
        }

        return builder.Close().Build();
    }

    public static MmPoint[] NormalizeWinding(IEnumerable<MmPoint> points)
    {
        MmPoint[] normalized = PolylineGeometryBuilder.Normalize(points);
        if (normalized.Length > 1 && normalized[0] == normalized[^1])
        {
            normalized = normalized[..^1];
        }

        if (normalized.Distinct().Count() < 3)
        {
            throw new InvalidOperationException("A polygon requires at least three distinct points.");
        }

        double signedArea = SignedArea(normalized);
        if (Math.Abs(signedArea) < 1e-9)
        {
            throw new InvalidOperationException("A polygon must have non-zero area.");
        }

        if (signedArea < 0)
        {
            Array.Reverse(normalized);
        }

        return normalized;
    }

    public static double SignedArea(IReadOnlyList<MmPoint> points)
    {
        double sum = 0;
        for (int index = 0; index < points.Count; index++)
        {
            MmPoint current = points[index];
            MmPoint next = points[(index + 1) % points.Count];
            sum += current.X * next.Y - next.X * current.Y;
        }

        return sum / 2;
    }
}
