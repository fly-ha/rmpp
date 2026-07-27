using Rmpp.Application.Abstractions;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Validation;

/// <summary>复用最终渲染测量逻辑检查字体、文本溢出、条码和元素类型错误。</summary>
public sealed class RenderingValidationRule(
    ElementSceneBuilder? elementSceneBuilder = null,
    IClock? clock = null) : IDocumentValidationRule
{
    private readonly ElementSceneBuilder elementSceneBuilder = elementSceneBuilder ?? new ElementSceneBuilder();
    private readonly IClock clock = clock ?? new SystemClock();

    public IEnumerable<ValidationIssue> Validate(DocumentValidationContext context)
    {
        RenderContext renderContext = new() { ReferenceTime = clock.LocalNow };
        foreach (Rmpp.Domain.Elements.TemplateElement element in context.Document.Elements)
        {
            ElementSceneBuildResult result = elementSceneBuilder.Build(element, renderContext);
            foreach (RenderIssue issue in result.Issues)
            {
                yield return new ValidationIssue(
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
                        DocumentId = context.Document.Id,
                        PageNumber = issue.PageNumber ?? 1,
                        ElementId = issue.ElementId ?? element.Id,
                    });
            }
        }
    }
}
