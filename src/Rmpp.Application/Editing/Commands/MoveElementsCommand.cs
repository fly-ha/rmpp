using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

/// <summary>按相同毫米增量移动全部未锁定元素。</summary>
public sealed class MoveElementsCommand(
    IEnumerable<Guid> elementIds,
    double deltaXmm,
    double deltaYmm,
    string? coalescingKey = null) : IEditorCommand
{
    private readonly HashSet<Guid> elementIds = ElementCommandUtilities.NormalizeIds(elementIds);

    public string Description => "Move elements";

    public string? CoalescingKey { get; } = coalescingKey;

    public TemplateDocument Execute(TemplateDocument document)
    {
        if (!double.IsFinite(deltaXmm) || !double.IsFinite(deltaYmm))
        {
            throw new InvalidOperationException("Move delta must be finite.");
        }

        if (deltaXmm == 0 && deltaYmm == 0)
        {
            return document;
        }

        return ElementCommandUtilities.Map(
            document,
            elementIds,
            element => element.IsLocked
                ? element
                : element with { Bounds = element.Bounds.Translate(deltaXmm, deltaYmm) });
    }
}
