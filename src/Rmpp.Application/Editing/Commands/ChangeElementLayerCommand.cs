using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

/// <summary>把未锁定元素移动到已存在的目标图层。</summary>
public sealed class ChangeElementLayerCommand(
    IEnumerable<Guid> elementIds,
    Guid targetLayerId) : IEditorCommand
{
    private readonly HashSet<Guid> elementIds = ElementCommandUtilities.NormalizeIds(elementIds);

    public string Description => "Change element layer";

    public TemplateDocument Execute(TemplateDocument document)
    {
        if (!document.Layers.Any(layer => layer.Id == targetLayerId))
        {
            throw new InvalidOperationException("Target layer does not exist.");
        }

        return ElementCommandUtilities.Map(
            document,
            elementIds,
            element => element.IsLocked || element.LayerId == targetLayerId
                ? element
                : element with { LayerId = targetLayerId });
    }
}
