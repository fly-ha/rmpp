using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Commands;

/// <summary>为每个未锁定元素设置绝对旋转角度。</summary>
public sealed class RotateElementsCommand(
    IReadOnlyDictionary<Guid, Angle> rotations,
    string? coalescingKey = null) : IEditorCommand
{
    private readonly IReadOnlyDictionary<Guid, Angle> rotations = rotations
        ?? throw new ArgumentNullException(nameof(rotations));

    public string Description => "Rotate elements";

    public string? CoalescingKey { get; } = coalescingKey;

    public TemplateDocument Execute(TemplateDocument document) =>
        ElementCommandUtilities.Map(
            document,
            rotations.Keys.ToHashSet(),
            element => element.IsLocked || element.Rotation == rotations[element.Id]
                ? element
                : element with { Rotation = rotations[element.Id] });
}
