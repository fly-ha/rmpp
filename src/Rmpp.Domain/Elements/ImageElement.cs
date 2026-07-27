using Rmpp.Domain.Data;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Elements;

public sealed record ImageElement : TemplateElement
{
    public Guid? AssetId { get; init; }
    public ElementExpression? VariablePath { get; init; }
    public ImageFitMode FitMode { get; init; } = ImageFitMode.Contain;
    public MmRect? Crop { get; init; }
    public StrokeStyle Border { get; init; } = StrokeStyle.None;
    public FillStyle Fill { get; init; } = FillStyle.None;
}
