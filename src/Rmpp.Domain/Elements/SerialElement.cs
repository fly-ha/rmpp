using Rmpp.Domain.Data;
using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Elements;

public sealed record SerialElement : TemplateElement
{
    public SerialDefinition Definition { get; init; } = new();
    public TextStyle TextStyle { get; init; } = new();
}
