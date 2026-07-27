using Rmpp.Application.Data;
using Rmpp.Application.Documents;
using Rmpp.Application.Validation;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Printing;

public sealed record PrintJobValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool HasBlockingErrors => Issues.Any(static issue => issue.BlocksOutput);
}

/// <summary>在最终输出前验证布局、绑定、资源和每个解析记录的实际渲染结果。</summary>
public sealed class PrintJobValidator(
    DocumentValidator? documentValidator = null,
    BindingValidationService? bindingValidator = null,
    ElementSceneBuilder? elementSceneBuilder = null)
{
    private static readonly HashSet<string> PerRecordRenderCodes = new(StringComparer.Ordinal)
    {
        "missing-font", "missing-glyph", "text-overflow", "text-expand-height",
        "invalid-retail-barcode", "invalid-check-digit", "invalid-itf", "invalid-codabar-length",
        "invalid-codabar-character", "invalid-code39-character", "barcode-too-long",
        "empty-barcode", "invalid-error-correction", "invalid-element",
    };

    private readonly DocumentValidator documentValidator = documentValidator ?? new DocumentValidator();
    private readonly BindingValidationService bindingValidator = bindingValidator ?? new BindingValidationService();
    private readonly ElementSceneBuilder elementSceneBuilder = elementSceneBuilder ?? new ElementSceneBuilder();

    public PrintJobValidationResult Validate(
        PrintJobRequest request,
        PrintJobPlan plan,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? assetContents = null,
        MmRect? printableArea = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        List<ValidationIssue> issues = [];
        issues.AddRange(LayoutValidationService.Validate(request));
        issues.AddRange(documentValidator.Validate(request.Document, assetContents)
            .Where(issue => !PerRecordRenderCodes.Contains(issue.Code)));
        if (request.DataSet is not null)
        {
            issues.AddRange(bindingValidator.Validate(request.Document, request.DataSet.Schema).Issues);
        }

        Dictionary<Guid, LayerDefinition> layers = request.Document.Layers.ToDictionary(static layer => layer.Id);
        foreach (PlannedPhysicalPage page in plan.Pages)
        {
            foreach (PlannedPlacement placement in page.Placements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                issues.AddRange(placement.Issues.Select(issue => issue with
                {
                    Location = issue.Location with { PageNumber = page.PageNumber, RecordIndex = placement.RecordIndex },
                }));
                RenderContext renderContext = new()
                {
                    Target = RenderTarget.Print,
                    ReferenceTime = plan.Context.JobTime.ReferenceTime,
                    PlacementTransform = placement.Transform,
                    ResolvedElements = placement.ResolvedElements.ToDictionary(static item => item.ElementId),
                };
                foreach (TemplateElement element in request.Document.Elements.Where(element =>
                             element.IsVisible && element.IsPrintable
                             && layers.TryGetValue(element.LayerId, out LayerDefinition? layer)
                             && layer.IsVisible && layer.IsPrintable))
                {
                    if (element is ImageElement { VariablePath: not null })
                    {
                        string? path = placement.ResolvedElements.FirstOrDefault(item => item.ElementId == element.Id)?.LocalImagePath;
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        {
                            issues.Add(new ValidationIssue(
                                "missing-variable-image",
                                "Resolved variable image path does not exist.",
                                ValidationSeverity.Error,
                                new ValidationLocation
                                {
                                    DocumentId = request.Document.Id,
                                    PageNumber = page.PageNumber,
                                    ElementId = element.Id,
                                    RecordIndex = placement.RecordIndex,
                                    PropertyPath = nameof(ImageElement.VariablePath),
                                }));
                        }
                    }

                    ElementSceneBuildResult result = elementSceneBuilder.Build(element, renderContext);
                    issues.AddRange(result.Issues.Select(issue => new ValidationIssue(
                        issue.Code,
                        issue.Message,
                        issue.Severity switch
                        {
                            RenderIssueSeverity.Error => ValidationSeverity.Error,
                            RenderIssueSeverity.Warning => ValidationSeverity.Warning,
                            _ => ValidationSeverity.Information,
                        },
                        new ValidationLocation
                        {
                            DocumentId = request.Document.Id,
                            PageNumber = page.PageNumber,
                            ElementId = element.Id,
                            RecordIndex = placement.RecordIndex,
                        })));
                    if (printableArea is MmRect area)
                    {
                        MmRect bounds = TransformBounds(element, placement.Transform);
                        if (bounds.X < area.X || bounds.Y < area.Y || bounds.Right > area.Right || bounds.Bottom > area.Bottom)
                        {
                            issues.Add(new ValidationIssue(
                                "printable-area-intrusion",
                                $"Element enters the printer hard margin by {Intrusion(bounds, area):0.###} mm.",
                                ValidationSeverity.Warning,
                                new ValidationLocation
                                {
                                    DocumentId = request.Document.Id,
                                    PageNumber = page.PageNumber,
                                    ElementId = element.Id,
                                    RecordIndex = placement.RecordIndex,
                                    PropertyPath = "Printer.PrintableArea",
                                }));
                        }
                    }
                }
            }
        }

        ValidationIssue[] ordered = issues
            .Distinct()
            .OrderByDescending(static issue => issue.Severity)
            .ThenBy(static issue => issue.Location.PageNumber)
            .ThenBy(static issue => issue.Location.RecordIndex)
            .ThenBy(static issue => issue.Location.ElementId)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
        return new PrintJobValidationResult(ordered);
    }

    private static MmRect TransformBounds(TemplateElement element, RenderTransform placement)
    {
        RenderTransform transform = RenderTransform.ForElement(element.Bounds, element.Rotation).Then(placement);
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

    private static double Intrusion(MmRect bounds, MmRect area) =>
        new[]
        {
            Math.Max(0, area.X - bounds.X),
            Math.Max(0, area.Y - bounds.Y),
            Math.Max(0, bounds.Right - area.Right),
            Math.Max(0, bounds.Bottom - area.Bottom),
        }.Max();
}
