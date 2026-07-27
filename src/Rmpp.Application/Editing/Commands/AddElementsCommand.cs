using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

/// <summary>向文档添加已分配稳定身份和有效图层的元素。</summary>
public sealed class AddElementsCommand(IEnumerable<TemplateElement> elements) : IEditorCommand
{
    private readonly TemplateElement[] elements = elements?.ToArray()
        ?? throw new ArgumentNullException(nameof(elements));

    public string Description => "Add elements";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (elements.Length == 0)
        {
            return document;
        }

        HashSet<Guid> ids = document.Elements.Select(static element => element.Id).ToHashSet();
        HashSet<Guid> layerIds = document.Layers.Select(static layer => layer.Id).ToHashSet();
        foreach (TemplateElement element in elements)
        {
            if (element.Id == Guid.Empty || !ids.Add(element.Id))
            {
                throw new InvalidOperationException("Added elements require unique non-empty identities.");
            }

            if (!layerIds.Contains(element.LayerId))
            {
                throw new InvalidOperationException("Added element references a layer that does not exist.");
            }
        }

        return document with { Elements = document.Elements.Concat(elements).ToArray() };
    }
}
