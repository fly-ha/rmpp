using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Geometry;

/// <summary>清理连续重复点并构造开放折线路径。</summary>
public static class PolylineGeometryBuilder
{
    public static RenderPath Build(IEnumerable<MmPoint> points)
    {
        MmPoint[] normalized = Normalize(points);
        if (normalized.Length < 2)
        {
            throw new InvalidOperationException("A polyline requires at least two distinct consecutive points.");
        }

        RenderPathBuilder builder = new();
        builder.MoveTo(normalized[0]);
        foreach (MmPoint point in normalized.Skip(1))
        {
            builder.LineTo(point);
        }

        return builder.Build();
    }

    internal static MmPoint[] Normalize(IEnumerable<MmPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        List<MmPoint> normalized = [];
        foreach (MmPoint point in points)
        {
            if (normalized.Count == 0 || normalized[^1] != point)
            {
                normalized.Add(point);
            }
        }

        return normalized.ToArray();
    }
}
