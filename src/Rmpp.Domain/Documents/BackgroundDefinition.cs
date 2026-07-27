using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Documents;

public sealed record BackgroundDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid AssetId { get; init; }
    public int PdfPageNumber { get; init; } = 1;
    public MmRect Bounds { get; init; }
    public ImageFitMode FitMode { get; init; } = ImageFitMode.Contain;
    public double Opacity { get; init; } = 1;
    public bool IsVisible { get; init; } = true;
    public bool IsPrintable { get; init; }
    public bool IsLocked { get; init; } = true;
}
