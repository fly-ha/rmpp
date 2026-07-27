using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Snapping;

public enum AlignmentMode
{
    Left,
    HorizontalCenter,
    Right,
    Top,
    VerticalCenter,
    Bottom,
}

/// <summary>为多选未锁定元素计算精确毫米对齐边界，不直接修改文档。</summary>
public sealed class AlignmentService
{
    public static IReadOnlyDictionary<Guid, MmRect> Calculate(
        TemplateDocument document,
        IEnumerable<Guid> elementIds,
        AlignmentMode mode)
    {
        ArgumentNullException.ThrowIfNull(document);
        HashSet<Guid> ids = elementIds?.ToHashSet() ?? throw new ArgumentNullException(nameof(elementIds));
        TemplateElement[] elements = document.Elements
            .Where(element => ids.Contains(element.Id) && !element.IsLocked)
            .ToArray();
        if (elements.Length < 2)
        {
            return new Dictionary<Guid, MmRect>();
        }

        double left = elements.Min(static element => element.Bounds.X);
        double right = elements.Max(static element => element.Bounds.Right);
        double top = elements.Min(static element => element.Bounds.Y);
        double bottom = elements.Max(static element => element.Bounds.Bottom);
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;
        return elements.ToDictionary(
            static element => element.Id,
            element => Align(element.Bounds, mode, left, right, top, bottom, centerX, centerY));
    }

    private static MmRect Align(
        MmRect bounds,
        AlignmentMode mode,
        double left,
        double right,
        double top,
        double bottom,
        double centerX,
        double centerY) => mode switch
        {
            AlignmentMode.Left => new MmRect(left, bounds.Y, bounds.Width, bounds.Height),
            AlignmentMode.HorizontalCenter => new MmRect(centerX - bounds.Width / 2, bounds.Y, bounds.Width, bounds.Height),
            AlignmentMode.Right => new MmRect(right - bounds.Width, bounds.Y, bounds.Width, bounds.Height),
            AlignmentMode.Top => new MmRect(bounds.X, top, bounds.Width, bounds.Height),
            AlignmentMode.VerticalCenter => new MmRect(bounds.X, centerY - bounds.Height / 2, bounds.Width, bounds.Height),
            AlignmentMode.Bottom => new MmRect(bounds.X, bottom - bounds.Height, bounds.Width, bounds.Height),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
}
