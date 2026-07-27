using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

public enum ElementOrderPlacement
{
    BringToFront,
    BringForward,
    SendBackward,
    SendToBack,
}

/// <summary>按稳定当前顺序调整元素层内前后关系，并重新生成连续 ZIndex。</summary>
public sealed class ReorderElementsCommand(
    IEnumerable<Guid> elementIds,
    ElementOrderPlacement placement) : IEditorCommand
{
    private readonly HashSet<Guid> elementIds = ElementCommandUtilities.NormalizeIds(elementIds);

    public string Description => "Reorder elements";

    public TemplateDocument Execute(TemplateDocument document)
    {
        List<TemplateElement> ordered = document.Elements
            .Select(static (element, index) => (element, index))
            .OrderBy(static item => item.element.ZIndex)
            .ThenBy(static item => item.index)
            .Select(static item => item.element)
            .ToList();
        TemplateElement[] original = ordered.ToArray();
        switch (placement)
        {
            case ElementOrderPlacement.BringToFront:
                MoveBlock(ordered, toFront: true);
                break;
            case ElementOrderPlacement.SendToBack:
                MoveBlock(ordered, toFront: false);
                break;
            case ElementOrderPlacement.BringForward:
                for (int index = ordered.Count - 2; index >= 0; index--)
                {
                    if (elementIds.Contains(ordered[index].Id) && !elementIds.Contains(ordered[index + 1].Id))
                    {
                        (ordered[index], ordered[index + 1]) = (ordered[index + 1], ordered[index]);
                    }
                }
                break;
            case ElementOrderPlacement.SendBackward:
                for (int index = 1; index < ordered.Count; index++)
                {
                    if (elementIds.Contains(ordered[index].Id) && !elementIds.Contains(ordered[index - 1].Id))
                    {
                        (ordered[index], ordered[index - 1]) = (ordered[index - 1], ordered[index]);
                    }
                }
                break;
        }

        if (ordered.SequenceEqual(original))
        {
            return document;
        }

        TemplateElement[] normalized = ordered
            .Select(static (element, index) => element with { ZIndex = index })
            .ToArray();
        return document with { Elements = normalized };
    }

    private void MoveBlock(List<TemplateElement> ordered, bool toFront)
    {
        List<TemplateElement> selected = ordered.Where(element => elementIds.Contains(element.Id)).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        ordered.RemoveAll(element => elementIds.Contains(element.Id));
        if (toFront)
        {
            ordered.AddRange(selected);
        }
        else
        {
            ordered.InsertRange(0, selected);
        }
    }
}
