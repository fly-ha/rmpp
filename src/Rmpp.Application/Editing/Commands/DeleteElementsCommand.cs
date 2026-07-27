using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

/// <summary>删除指定元素；不存在的身份被安全忽略。</summary>
public sealed class DeleteElementsCommand(IEnumerable<Guid> elementIds) : IEditorCommand
{
    private readonly HashSet<Guid> elementIds = ElementCommandUtilities.NormalizeIds(elementIds);

    public string Description => "Delete elements";

    public TemplateDocument Execute(TemplateDocument document)
    {
        Rmpp.Domain.Elements.TemplateElement[] retained = document.Elements
            .Where(element => !elementIds.Contains(element.Id))
            .ToArray();
        return retained.Length == document.Elements.Count
            ? document
            : document with { Elements = retained };
    }
}
