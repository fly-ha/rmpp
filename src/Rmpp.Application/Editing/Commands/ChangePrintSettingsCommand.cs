using Rmpp.Domain.Documents;
using Rmpp.Domain.Printing;

namespace Rmpp.Application.Editing.Commands;

/// <summary>把与硬件无关的打印默认值保存进模板，并纳入统一撤销/重做历史。</summary>
public sealed class ChangePrintSettingsCommand(TemplatePrintSettings settings) : IEditorCommand
{
    private readonly TemplatePrintSettings settings = settings?.Validate()
        ?? throw new ArgumentNullException(nameof(settings));

    public string Description => "保存模板打印设置";
    public string? CoalescingKey => "template-print-settings";

    public TemplateDocument Execute(TemplateDocument document) =>
        document.PrintSettings == settings ? document : document with { PrintSettings = settings };
}
