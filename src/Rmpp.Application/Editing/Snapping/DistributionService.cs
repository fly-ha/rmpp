using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Snapping;

public enum DistributionAxis
{
    Horizontal,
    Vertical,
}

/// <summary>在保持最外侧元素位置的前提下计算等间隙分布。</summary>
public sealed class DistributionService
{
    public static IReadOnlyDictionary<Guid, MmRect> Calculate(
        TemplateDocument document,
        IEnumerable<Guid> elementIds,
        DistributionAxis axis)
    {
        ArgumentNullException.ThrowIfNull(document);
        HashSet<Guid> ids = elementIds?.ToHashSet() ?? throw new ArgumentNullException(nameof(elementIds));
        TemplateElement[] ordered = document.Elements
            .Where(element => ids.Contains(element.Id) && !element.IsLocked)
            .OrderBy(element => axis == DistributionAxis.Horizontal ? element.Bounds.X : element.Bounds.Y)
            .ToArray();
        if (ordered.Length < 3)
        {
            return new Dictionary<Guid, MmRect>();
        }

        double start = axis == DistributionAxis.Horizontal ? ordered[0].Bounds.X : ordered[0].Bounds.Y;
        double end = axis == DistributionAxis.Horizontal ? ordered[^1].Bounds.Right : ordered[^1].Bounds.Bottom;
        double occupied = ordered.Sum(element => axis == DistributionAxis.Horizontal
            ? element.Bounds.Width
            : element.Bounds.Height);
        double gap = (end - start - occupied) / (ordered.Length - 1);
        double cursor = start;
        Dictionary<Guid, MmRect> result = [];
        foreach (TemplateElement element in ordered)
        {
            MmRect bounds = axis == DistributionAxis.Horizontal
                ? new MmRect(cursor, element.Bounds.Y, element.Bounds.Width, element.Bounds.Height)
                : new MmRect(element.Bounds.X, cursor, element.Bounds.Width, element.Bounds.Height);
            result[element.Id] = bounds;
            cursor += (axis == DistributionAxis.Horizontal ? element.Bounds.Width : element.Bounds.Height) + gap;
        }

        return result;
    }
}
