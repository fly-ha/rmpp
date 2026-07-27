using Rmpp.Domain.Documents;

namespace Rmpp.Application.Validation;

/// <summary>包含一次文档验证所需的不可变文档和会话资源。</summary>
public sealed record DocumentValidationContext(
    TemplateDocument Document,
    IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> AssetContents);

public interface IDocumentValidationRule
{
    IEnumerable<ValidationIssue> Validate(DocumentValidationContext context);
}
