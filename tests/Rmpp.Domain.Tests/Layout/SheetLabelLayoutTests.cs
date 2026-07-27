using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Xunit;

namespace Rmpp.Domain.Tests.Layout;

public sealed class SheetLabelLayoutTests
{
    [Fact]
    public void StartingCellMustFitGrid()
    {
        SheetLabelLayout layout = new()
        {
            LabelSize = new MmSize(20, 10),
            Margins = new MmThickness(1),
            Rows = 2,
            Columns = 2,
            StartingCell = 5,
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Validate());
    }
}
