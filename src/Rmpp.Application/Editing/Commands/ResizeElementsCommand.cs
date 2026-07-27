using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Commands;

/// <summary>设置元素新边界，并同步缩放直线、折线和多边形的局部点。</summary>
public sealed class ResizeElementsCommand(
    IReadOnlyDictionary<Guid, MmRect> boundsByElement,
    string? coalescingKey = null) : IEditorCommand
{
    private readonly IReadOnlyDictionary<Guid, MmRect> boundsByElement = boundsByElement
        ?? throw new ArgumentNullException(nameof(boundsByElement));

    public string Description => "Resize elements";

    public string? CoalescingKey { get; } = coalescingKey;

    public TemplateDocument Execute(TemplateDocument document)
    {
        if (boundsByElement.Count == 0)
        {
            return document;
        }

        return ElementCommandUtilities.Map(
            document,
            boundsByElement.Keys.ToHashSet(),
            element => Resize(element, boundsByElement[element.Id]));
    }

    private static TemplateElement Resize(TemplateElement element, MmRect bounds)
    {
        if (element.IsLocked)
        {
            return element;
        }

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Element bounds must remain positive.");
        }

        if (element.Bounds == bounds)
        {
            return element;
        }

        double scaleX = element.Bounds.Width == 0 ? 1 : bounds.Width / element.Bounds.Width;
        double scaleY = element.Bounds.Height == 0 ? 1 : bounds.Height / element.Bounds.Height;
        MmPoint Scale(MmPoint point) => new(point.X * scaleX, point.Y * scaleY);
        return element switch
        {
            LineElement line => line with { Bounds = bounds, Start = Scale(line.Start), End = Scale(line.End) },
            PolylineElement polyline => polyline with { Bounds = bounds, Points = polyline.Points.Select(Scale).ToArray() },
            PolygonElement polygon => polygon with { Bounds = bounds, Points = polygon.Points.Select(Scale).ToArray() },
            _ => element with { Bounds = bounds },
        };
    }
}
