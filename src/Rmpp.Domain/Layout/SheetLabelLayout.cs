using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Layout;

public sealed record SheetLabelLayout : DocumentLayout
{
    public required MmSize LabelSize { get; init; }
    public required MmThickness Margins { get; init; }
    public int Rows { get; init; } = 1;
    public int Columns { get; init; } = 1;
    public double HorizontalGapMm { get; init; }
    public double VerticalGapMm { get; init; }
    public TraversalOrder TraversalOrder { get; init; } = TraversalOrder.RowMajor;
    public int StartingCell { get; init; } = 1;

    public SheetLabelLayout Validate()
    {
        if (Rows <= 0 || Columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Rows), "Rows and columns must be positive.");
        }

        if (StartingCell < 1 || StartingCell > Rows * Columns)
        {
            throw new ArgumentOutOfRangeException(nameof(StartingCell));
        }

        if (HorizontalGapMm < 0 || VerticalGapMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(HorizontalGapMm), "Gaps must be non-negative.");
        }

        return this;
    }
}
