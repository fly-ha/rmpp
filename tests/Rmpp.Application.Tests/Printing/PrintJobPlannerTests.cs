using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Xunit;

namespace Rmpp.Application.Tests.Printing;

public sealed class PrintJobPlannerTests
{
    [Fact]
    public void SinglePageSelectionCopiesSerialsAndJobTimeAreFrozen()
    {
        TextElement text = new()
        {
            Bounds = new MmRect(0, 0, 40, 10),
            Content = new ElementExpression("=[Id]"),
        };
        SerialElement serial = new()
        {
            Bounds = new MmRect(0, 15, 30, 10),
            Definition = new SerialDefinition { Start = 10, Step = 1, AdvancePerCopy = true },
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(text, serial);
        DataSetSnapshot data = DataSet(3);
        DateTimeOffset reference = new(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(8));

        PrintJobPlan plan = new PrintJobPlanner(new DataPreviewService(), new FakeClock()).Plan(new PrintJobRequest
        {
            Document = document,
            DataSet = data,
            RecordSelection = new PrintRecordSelection { StartIndex = 1, EndIndexInclusive = 2 },
            CopyPolicy = new PrintCopyPolicy { RecordCopies = 2, JobCopies = 2 },
            ReferenceTime = reference,
        });

        Assert.Equal(8, plan.Pages.Count);
        Assert.Equal(8, plan.TotalPlacements);
        Assert.Equal(reference, plan.Context.JobTime.ReferenceTime);
        PlannedPlacement first = plan.Pages[0].Placements[0];
        PlannedPlacement secondCopy = plan.Pages[1].Placements[0];
        PlannedPlacement repeatedJob = plan.Pages[4].Placements[0];
        Assert.Equal(1, first.RecordIndex);
        Assert.Equal("10", first.ResolvedElements.Single(item => item.ElementId == serial.Id).Text);
        Assert.Equal("11", secondCopy.ResolvedElements.Single(item => item.ElementId == serial.Id).Text);
        Assert.Equal("10", repeatedJob.ResolvedElements.Single(item => item.ElementId == serial.Id).Text);
        Assert.Equal("R1", first.ResolvedElements.Single(item => item.ElementId == text.Id).Text);
    }

    [Fact]
    public void SheetStartingCellAndTraversalProducePartialPages()
    {
        SheetLabelLayout layout = new()
        {
            LabelSize = new MmSize(30, 20),
            Margins = new MmThickness(5),
            Rows = 2,
            Columns = 3,
            HorizontalGapMm = 2,
            VerticalGapMm = 3,
            StartingCell = 2,
            TraversalOrder = TraversalOrder.RowMajor,
        };
        (TemplateDocument source, _) = TestDocumentFactory.Create(TestDocumentFactory.Rectangle(width: 10, height: 5));
        TemplateDocument document = source with
        {
            Page = source.Page with
            {
                Media = new MediaDefinition("Sheet", new MmSize(110, 60)),
                Layout = layout,
            },
        };

        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest
        {
            Document = document,
            DataSet = DataSet(7),
        });

        Assert.Equal(2, plan.Pages.Count);
        Assert.Equal([2, 3, 4, 5, 6], plan.Pages[0].Placements.Select(static placement => placement.Cell.Index));
        Assert.Equal([1, 2], plan.Pages[1].Placements.Select(static placement => placement.Cell.Index));
        Assert.Equal(new MmPoint(37, 5), plan.Pages[0].Placements[0].Transform.Transform(new MmPoint(0, 0)));
    }

    [Fact]
    public void ColumnMajorUsesPhysicalCellNumbersInColumnOrder()
    {
        SheetLabelLayout layout = new()
        {
            LabelSize = new MmSize(20, 10),
            Margins = new MmThickness(0),
            Rows = 2,
            Columns = 3,
            StartingCell = 2,
            TraversalOrder = TraversalOrder.ColumnMajor,
        };
        (TemplateDocument source, _) = TestDocumentFactory.Create();
        TemplateDocument document = source with { Page = source.Page with { Layout = layout } };

        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest
        {
            Document = document,
            DataSet = DataSet(4),
        });

        Assert.Equal([2, 5, 3, 6], Assert.Single(plan.Pages).Placements.Select(static placement => placement.Cell.Index));
    }

    [Fact]
    public void ThousandsOfRecordsPlanDeterministicallyAndCancellationStops()
    {
        (TemplateDocument document, _) = TestDocumentFactory.Create();
        DataSetSnapshot data = DataSet(2_000);
        FakeClock clock = new();
        PrintJobRequest request = new() { Document = document, DataSet = data };
        PrintJobPlan first = new PrintJobPlanner(clock: clock).Plan(request);
        PrintJobPlan second = new PrintJobPlanner(clock: clock).Plan(request);

        Assert.Equal(2_000, first.Pages.Count);
        Assert.Equal(
            first.Pages.SelectMany(static page => page.Placements).Select(static placement => placement.RecordIndex),
            second.Pages.SelectMany(static page => page.Placements).Select(static placement => placement.RecordIndex));
        Assert.Equal(first.Context.JobTime.ReferenceTime, second.Context.JobTime.ReferenceTime);

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new PrintJobPlanner().Plan(request, cancellation.Token));
    }

    [Fact]
    public void RollLabelsUseLabelPhysicalPageSize()
    {
        (TemplateDocument source, _) = TestDocumentFactory.Create();
        TemplateDocument document = source with
        {
            Page = source.Page with
            {
                Layout = new RollLabelLayout { LabelSize = new MmSize(50, 30), GapMm = 2 },
            },
        };

        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest
        {
            Document = document,
            DataSet = DataSet(2),
        });

        Assert.All(plan.Pages, page => Assert.Equal(new MmSize(50, 30), page.Size));
    }

    private static DataSetSnapshot DataSet(int rows) => new()
    {
        SourceDisplayName = "records.csv",
        Schema = new DataSchema { Columns = [new DataColumnDefinition("Id", 0)] },
        Rows = Enumerable.Range(0, rows).Select(index => new DataRowSnapshot
        {
            Index = index,
            Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["Id"] = $"R{index}" },
        }).ToArray(),
    };

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 27, 4, 0, 0, TimeSpan.Zero);

        public DateTimeOffset LocalNow { get; } = new(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(8));
    }
}
