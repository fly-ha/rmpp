using Rmpp.Application.Validation;
using Rmpp.Domain.Documents;

namespace Rmpp.Application.Documents;

/// <summary>聚合应用层验证规则并稳定排序问题，供会话、预览和打印门禁复用。</summary>
public sealed class DocumentValidator
{
    private readonly IReadOnlyList<IDocumentValidationRule> rules;

    public DocumentValidator(IEnumerable<IDocumentValidationRule>? rules = null)
    {
        this.rules = (rules ?? DefaultRules()).ToArray();
    }

    public IReadOnlyList<ValidationIssue> Validate(
        TemplateDocument document,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? assetContents = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        DocumentValidationContext context = new(
            document,
            assetContents ?? new Dictionary<Guid, ReadOnlyMemory<byte>>());
        return rules
            .SelectMany(rule => rule.Validate(context))
            .OrderByDescending(static issue => issue.Severity)
            .ThenBy(static issue => issue.Location.PageNumber)
            .ThenBy(static issue => issue.Location.ElementId)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<IDocumentValidationRule> DefaultRules()
    {
        yield return new GeometryValidationRule();
        yield return new LayerValidationRule();
        yield return new AssetValidationRule();
        yield return new RenderingValidationRule();
        yield return new PrintableBoundsValidationRule();
    }
}
