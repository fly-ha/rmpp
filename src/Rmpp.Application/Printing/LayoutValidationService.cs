using Rmpp.Application.Validation;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Printing;

/// <summary>检查物理介质、标签单元格和模板内容尺寸是否能够形成有效布局。</summary>
public sealed class LayoutValidationService
{
    public static IReadOnlyList<ValidationIssue> Validate(PrintJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<ValidationIssue> issues = [];
        MmSize pageSize = OrientedSize(request.Document.Page.Media);
        MmSize contentSize = pageSize;
        try
        {
            switch (request.Document.Page.Layout)
            {
                case SinglePageLayout:
                    break;
                case RollLabelLayout roll:
                    contentSize = roll.LabelSize;
                    if (roll.LabelSize.IsEmpty || !double.IsFinite(roll.GapMm) || roll.GapMm < 0)
                    {
                        issues.Add(DocumentIssue(request.Document.Id, "invalid-roll-layout", "Roll label size or gap is invalid."));
                    }
                    break;
                case SheetLabelLayout sheet:
                    contentSize = sheet.LabelSize;
                    sheet.Validate();
                    double requiredWidth = sheet.Margins.Left + sheet.Margins.Right
                        + sheet.Columns * sheet.LabelSize.Width
                        + Math.Max(0, sheet.Columns - 1) * sheet.HorizontalGapMm;
                    double requiredHeight = sheet.Margins.Top + sheet.Margins.Bottom
                        + sheet.Rows * sheet.LabelSize.Height
                        + Math.Max(0, sheet.Rows - 1) * sheet.VerticalGapMm;
                    if (requiredWidth > pageSize.Width + 0.0001 || requiredHeight > pageSize.Height + 0.0001)
                    {
                        issues.Add(DocumentIssue(
                            request.Document.Id,
                            "sheet-layout-overflow",
                            $"Label grid requires {requiredWidth:0.###} × {requiredHeight:0.###} mm but media is {pageSize.Width:0.###} × {pageSize.Height:0.###} mm."));
                    }

                    int startingCell = request.StartingCellOverride ?? sheet.StartingCell;
                    if (startingCell < 1 || startingCell > sheet.Rows * sheet.Columns)
                    {
                        issues.Add(DocumentIssue(request.Document.Id, "invalid-starting-cell", "Starting label cell is outside the sheet."));
                    }
                    break;
                default:
                    issues.Add(DocumentIssue(request.Document.Id, "unsupported-layout", "Document layout is not supported."));
                    break;
            }
        }
        catch (ArgumentException exception)
        {
            issues.Add(DocumentIssue(request.Document.Id, "invalid-layout", exception.Message));
        }

        foreach (TemplateElement element in request.Document.Elements.Where(static element => element.IsPrintable))
        {
            MmRect physical = RotatedBounds(element);
            if (physical.X < 0 || physical.Y < 0 || physical.Right > contentSize.Width || physical.Bottom > contentSize.Height)
            {
                issues.Add(new ValidationIssue(
                    "element-outside-content",
                    "Printable element extends outside the template content area.",
                    ValidationSeverity.Error,
                    new ValidationLocation
                    {
                        DocumentId = request.Document.Id,
                        PageNumber = 1,
                        ElementId = element.Id,
                        PropertyPath = nameof(element.Bounds),
                    }));
            }
        }

        foreach (BackgroundDefinition background in request.Document.Backgrounds.Where(static background => background.IsPrintable))
        {
            if (background.Bounds.X < 0 || background.Bounds.Y < 0
                || background.Bounds.Right > contentSize.Width || background.Bounds.Bottom > contentSize.Height)
            {
                issues.Add(new ValidationIssue(
                    "background-outside-content",
                    "Printable background extends outside the template content area.",
                    ValidationSeverity.Error,
                    new ValidationLocation
                    {
                        DocumentId = request.Document.Id,
                        PageNumber = 1,
                        BackgroundId = background.Id,
                        PropertyPath = nameof(background.Bounds),
                    }));
            }
        }

        return issues;
    }

    internal static MmRect RotatedBounds(TemplateElement element)
    {
        RenderTransform transform = RenderTransform.ForElement(element.Bounds, element.Rotation);
        MmPoint[] points =
        [
            transform.Transform(new MmPoint(0, 0)),
            transform.Transform(new MmPoint(element.Bounds.Width, 0)),
            transform.Transform(new MmPoint(element.Bounds.Width, element.Bounds.Height)),
            transform.Transform(new MmPoint(0, element.Bounds.Height)),
        ];
        double left = points.Min(static point => point.X);
        double top = points.Min(static point => point.Y);
        double right = points.Max(static point => point.X);
        double bottom = points.Max(static point => point.Y);
        return new MmRect(left, top, right - left, bottom - top);
    }

    private static MmSize OrientedSize(MediaDefinition media) =>
        media.Orientation == PageOrientation.Landscape
            ? new MmSize(media.Size.Height, media.Size.Width)
            : media.Size;

    private static ValidationIssue DocumentIssue(Guid documentId, string code, string message) =>
        new(
            code,
            message,
            ValidationSeverity.Error,
            new ValidationLocation { DocumentId = documentId, PageNumber = 1, PropertyPath = "Page.Layout" });
}
