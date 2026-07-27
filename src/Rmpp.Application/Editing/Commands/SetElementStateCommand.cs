using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing.Commands;

/// <summary>统一修改锁定、可见和可打印状态；显式锁定操作允许解锁已有元素。</summary>
public sealed class SetElementStateCommand(
    IEnumerable<Guid> elementIds,
    bool? isLocked = null,
    bool? isVisible = null,
    bool? isPrintable = null) : IEditorCommand
{
    private readonly HashSet<Guid> elementIds = ElementCommandUtilities.NormalizeIds(elementIds);

    public string Description => "Change element state";

    public TemplateDocument Execute(TemplateDocument document) =>
        ElementCommandUtilities.Map(
            document,
            elementIds,
            element => element with
            {
                IsLocked = isLocked ?? element.IsLocked,
                IsVisible = isVisible ?? element.IsVisible,
                IsPrintable = isPrintable ?? element.IsPrintable,
            });
}
