using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Application.Tests.Printing;

public sealed class PrintPreviewServiceTests
{
    [Fact]
    public async Task SheetPreviewCombinesPlacementCommandsOnPhysicalPage()
    {
        RectangleElement element = TestDocumentFactory.Rectangle(x: 1, y: 2, width: 10, height: 5);
        (TemplateDocument source, _) = TestDocumentFactory.Create(element);
        TemplateDocument document = source with
        {
            Page = source.Page with
            {
                Media = new MediaDefinition("Sheet", new MmSize(110, 30)),
                Layout = new SheetLabelLayout
                {
                    LabelSize = new MmSize(50, 30),
                    Margins = new MmThickness(0),
                    Rows = 1,
                    Columns = 2,
                    HorizontalGapMm = 10,
                },
            },
        };
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest
        {
            Document = document,
            DataSet = DataSet(2),
        });

        RenderPage page = Assert.Single((await new PrintPreviewService().GetPageAsync(plan, 0)).Pages);

        Assert.Equal(new MmSize(110, 30), page.Size);
        Assert.Equal(2, page.Commands.Count);
        Assert.Equal(new MmPoint(1, 2), page.Commands[0].Transform.Transform(new MmPoint(0, 0)));
        Assert.Equal(new MmPoint(61, 2), page.Commands[1].Transform.Transform(new MmPoint(0, 0)));
    }

    [Fact]
    public async Task LruCacheIsBoundedAndEvictedPageIsRebuilt()
    {
        (TemplateDocument document, _) = TestDocumentFactory.Create(TestDocumentFactory.Rectangle());
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest
        {
            Document = document,
            DataSet = DataSet(3),
        });
        PrintPreviewService preview = new(maximumCachedPages: 2);

        await preview.GetPageAsync(plan, 0);
        await preview.GetPageAsync(plan, 1);
        await preview.GetPageAsync(plan, 2);
        await preview.GetPageAsync(plan, 1);
        await preview.GetPageAsync(plan, 0);

        Assert.Equal(2, preview.CachedPageCount);
        Assert.Equal(4, preview.BuiltPageCount);
    }

    [Fact]
    public async Task CancelledPreviewDoesNotPopulateCache()
    {
        (TemplateDocument document, _) = TestDocumentFactory.Create(TestDocumentFactory.Rectangle());
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest { Document = document });
        PrintPreviewService preview = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview.GetPageAsync(plan, 0, cancellation.Token));
        Assert.Equal(0, preview.CachedPageCount);
    }

    private static DataSetSnapshot DataSet(int rows) => new()
    {
        SourceDisplayName = "preview.csv",
        Schema = new DataSchema { Columns = [new DataColumnDefinition("Id", 0)] },
        Rows = Enumerable.Range(0, rows).Select(index => new DataRowSnapshot
        {
            Index = index,
            Values = new Dictionary<string, string?> { ["Id"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        }).ToArray(),
    };
}
