using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Validation;

/// <summary>检查重复身份、空尺寸以及局部路径点的有限物理几何。</summary>
public sealed class GeometryValidationRule : IDocumentValidationRule
{
    public IEnumerable<ValidationIssue> Validate(DocumentValidationContext context)
    {
        HashSet<Guid> ids = [];
        foreach (TemplateElement element in context.Document.Elements)
        {
            ValidationLocation location = new()
            {
                DocumentId = context.Document.Id,
                PageNumber = 1,
                ElementId = element.Id,
            };
            if (!ids.Add(element.Id))
            {
                yield return new ValidationIssue(
                    "duplicate-element-id",
                    "Element identity is duplicated.",
                    ValidationSeverity.Error,
                    location);
            }

            if (element.Bounds.Width <= 0 || element.Bounds.Height <= 0)
            {
                yield return new ValidationIssue(
                    "empty-element-bounds",
                    "Element bounds must have positive width and height.",
                    ValidationSeverity.Error,
                    location with { PropertyPath = nameof(element.Bounds) });
            }

            IEnumerable<MmPoint> points = element switch
            {
                LineElement line => [line.Start, line.End],
                PathElement path => path.Points,
                _ => Array.Empty<MmPoint>(),
            };
            foreach (MmPoint point in points)
            {
                if (point.X < 0 || point.Y < 0 || point.X > element.Bounds.Width || point.Y > element.Bounds.Height)
                {
                    yield return new ValidationIssue(
                        "point-outside-local-bounds",
                        "A path point is outside the element's local bounds.",
                        ValidationSeverity.Warning,
                        location with { PropertyPath = "Points" });
                    break;
                }
            }
        }
    }
}
