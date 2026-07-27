using Rmpp.Domain.Data;
using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Elements;

public sealed record TextElement : TemplateElement
{
    public ElementExpression Content { get; init; } = ElementExpression.Literal("Text");
    public TextStyle TextStyle { get; init; } = new();
    public StrokeStyle Border { get; init; } = StrokeStyle.None;
    public FillStyle Fill { get; init; } = FillStyle.None;
}
