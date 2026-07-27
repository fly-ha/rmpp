using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

/// <summary>以一个命令替换参考线集合，供新增、移动和删除操作共享撤销历史。</summary>
public sealed class ChangeGuidesCommand(IReadOnlyList<GuideDefinition> guides) : IEditorCommand
{
    private readonly GuideDefinition[] guides = guides?.ToArray()
        ?? throw new ArgumentNullException(nameof(guides));

    public string Description => "Change guides";

    public TemplateDocument Execute(TemplateDocument document)
    {
        if (guides.Any(static guide => guide.Id == Guid.Empty || !double.IsFinite(guide.PositionMm))
            || guides.Select(static guide => guide.Id).Distinct().Count() != guides.Length)
        {
            throw new InvalidOperationException("Guides require unique identities and finite positions.");
        }

        return document.Guides.SequenceEqual(guides)
            ? document
            : document with { Guides = guides };
    }
}
