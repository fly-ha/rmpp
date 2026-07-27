using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Desktop.Controls;

/// <summary>在毫米空间按最高 ZIndex 优先命中可见、未隐藏图层中的元素。</summary>
public static class ElementHitTester
{
    public static TemplateElement? HitTest(TemplateDocument document, MmPoint point, double toleranceMm = 1)
    {
        HashSet<Guid> visibleLayers = document.Layers.Where(static layer => layer.IsVisible).Select(static layer => layer.Id).ToHashSet();
        return document.Elements
            .Where(element => element.IsVisible && visibleLayers.Contains(element.LayerId) && Contains(element.Bounds, point, toleranceMm))
            .OrderByDescending(static element => element.ZIndex)
            .ThenByDescending(static element => element.Id)
            .FirstOrDefault();
    }

    private static bool Contains(MmRect bounds, MmPoint point, double tolerance) =>
        point.X >= bounds.X - tolerance && point.X <= bounds.Right + tolerance
        && point.Y >= bounds.Y - tolerance && point.Y <= bounds.Bottom + tolerance;
}
