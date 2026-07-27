using Rmpp.Domain.Data;
using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Elements;

public sealed record DateTimeElement : TemplateElement
{
    public DateTimeDefinition Definition { get; init; } = new();
    public TextStyle TextStyle { get; init; } = new();
}
