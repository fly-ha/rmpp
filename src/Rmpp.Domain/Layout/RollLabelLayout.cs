using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Layout;

public sealed record RollLabelLayout : DocumentLayout
{
    public required MmSize LabelSize { get; init; }
    public double GapMm { get; init; }
}
