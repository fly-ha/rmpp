using Rmpp.Domain.Documents;
using Rmpp.Domain.Layout;

namespace Rmpp.Application.Editing.Commands;

/// <summary>修改模板物理页面定义，确保尺寸与方向变化可撤销且不依赖打印机对象。</summary>
public sealed class ChangePageDefinitionCommand(PageDefinition page) : IEditorCommand
{
    private readonly PageDefinition page = page ?? throw new ArgumentNullException(nameof(page));

    public string Description => "修改模板页面设置";
    public string? CoalescingKey => "page-settings";

    public TemplateDocument Execute(TemplateDocument document) =>
        document.Page == page ? document : document with { Page = page };
}
