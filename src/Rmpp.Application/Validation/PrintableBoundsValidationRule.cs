using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Validation;

/// <summary>按旋转后的物理包围盒检查可打印元素是否超出页面。</summary>
public sealed class PrintableBoundsValidationRule : IDocumentValidationRule
{
    public IEnumerable<ValidationIssue> Validate(DocumentValidationContext context)
    {
        MmSize media = context.Document.Page.Media.Orientation == PageOrientation.Landscape
            ? new MmSize(context.Document.Page.Media.Size.Height, context.Document.Page.Media.Size.Width)
            : context.Document.Page.Media.Size;
        MmRect page = new(0, 0, media.Width, media.Height);
        HashSet<Guid> printableLayers = context.Document.Layers
            .Where(static layer => layer.IsPrintable)
            .Select(static layer => layer.Id)
            .ToHashSet();
        foreach (TemplateElement element in context.Document.Elements.Where(element =>
                     element.IsPrintable && printableLayers.Contains(element.LayerId)))
        {
            MmRect physical = RotatedBounds(element);
            if (physical.X < page.X || physical.Y < page.Y || physical.Right > page.Right || physical.Bottom > page.Bottom)
            {
                yield return new ValidationIssue(
                    "outside-printable-page",
                    "Printable element extends outside the physical page.",
                    ValidationSeverity.Warning,
                    new ValidationLocation
                    {
                        DocumentId = context.Document.Id,
                        PageNumber = 1,
                        ElementId = element.Id,
                        PropertyPath = nameof(element.Bounds),
                    });
            }
        }
    }

    private static MmRect RotatedBounds(TemplateElement element)
    {
        RenderTransform transform = RenderTransform.ForElement(element.Bounds, element.Rotation);
        MmPoint[] corners =
        [
            transform.Transform(new MmPoint(0, 0)),
            transform.Transform(new MmPoint(element.Bounds.Width, 0)),
            transform.Transform(new MmPoint(element.Bounds.Width, element.Bounds.Height)),
            transform.Transform(new MmPoint(0, element.Bounds.Height)),
        ];
        double left = corners.Min(static point => point.X);
        double top = corners.Min(static point => point.Y);
        double right = corners.Max(static point => point.X);
        double bottom = corners.Max(static point => point.Y);
        return new MmRect(left, top, right - left, bottom - top);
    }
}
