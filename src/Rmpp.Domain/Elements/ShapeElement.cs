using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Elements;

public abstract record ShapeElement : TemplateElement
{
    public StrokeStyle Stroke { get; init; } = new();
    public FillStyle Fill { get; init; } = FillStyle.None;
}
