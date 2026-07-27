using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Commands;

/// <summary>替换直线、折线或多边形的类型适配局部控制点。</summary>
public sealed class EditPointsCommand(
    Guid elementId,
    IEnumerable<MmPoint> points,
    string? coalescingKey = null) : IEditorCommand
{
    private readonly MmPoint[] points = points?.ToArray() ?? throw new ArgumentNullException(nameof(points));

    public string Description => "Edit points";

    public string? CoalescingKey { get; } = coalescingKey;

    public TemplateDocument Execute(TemplateDocument document) =>
        ElementCommandUtilities.Map(
            document,
            new HashSet<Guid> { elementId },
            element => element.IsLocked ? element : ReplacePoints(element));

    private TemplateElement ReplacePoints(TemplateElement element) => element switch
    {
        LineElement line when points.Length == 2 => line with { Start = points[0], End = points[1] },
        PolylineElement polyline when points.Length >= 2 => polyline with { Points = points },
        PolygonElement polygon when points.Distinct().Count() >= 3 => polygon with { Points = points },
        LineElement => throw new InvalidOperationException("A line requires exactly two points."),
        PolylineElement => throw new InvalidOperationException("A polyline requires at least two points."),
        PolygonElement => throw new InvalidOperationException("A polygon requires at least three distinct points."),
        _ => throw new InvalidOperationException("The selected element does not support point editing."),
    };
}
