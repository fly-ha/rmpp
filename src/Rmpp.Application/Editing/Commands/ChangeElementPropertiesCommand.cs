using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

/// <summary>通过受控变换同步修改属性面板中的一个或多个元素。</summary>
public sealed class ChangeElementPropertiesCommand(
    IEnumerable<Guid> elementIds,
    Func<TemplateElement, TemplateElement> transform,
    string description = "Change element properties",
    string? coalescingKey = null) : IEditorCommand
{
    private readonly HashSet<Guid> elementIds = ElementCommandUtilities.NormalizeIds(elementIds);
    private readonly Func<TemplateElement, TemplateElement> transform = transform
        ?? throw new ArgumentNullException(nameof(transform));

    public string Description { get; } = description;

    public string? CoalescingKey { get; } = coalescingKey;

    public TemplateDocument Execute(TemplateDocument document) =>
        ElementCommandUtilities.Map(document, elementIds, Transform);

    private TemplateElement Transform(TemplateElement element)
    {
        if (element.IsLocked)
        {
            return element;
        }

        TemplateElement replacement = transform(element);
        if (replacement.Id != element.Id)
        {
            throw new InvalidOperationException("Property changes cannot replace element identity.");
        }

        return replacement;
    }
}
