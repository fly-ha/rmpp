using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing;

/// <summary>描述一次从旧文档快照到新快照的确定性编辑变换。</summary>
public interface IEditorCommand
{
    string Description { get; }

    string? CoalescingKey => null;

    TemplateDocument Execute(TemplateDocument document);
}
