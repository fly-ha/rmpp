using System.Globalization;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Printing;

/// <summary>把记录、副本和布局冻结为有序物理页，过程中不渲染位图或访问打印驱动。</summary>
public sealed class PrintJobPlanner(
    DataPreviewService? dataPreviewService = null,
    IClock? clock = null)
{
    private readonly DataPreviewService dataPreviewService = dataPreviewService ?? new DataPreviewService();
    private readonly IClock clock = clock ?? new SystemClock();

    public PrintJobPlan Plan(PrintJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        PrintCopyPolicy copies = request.CopyPolicy.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.OutputCount);
        DataSetSnapshot dataSet = request.DataSet ?? FixedDataSet(request.OutputCount);
        // 无数据模式的逻辑记录完全由输出数量生成，不能再被仅用于外部数据的记录范围截断。
        IReadOnlyList<int> selectedIndices = request.DataSet is null
            ? Enumerable.Range(0, request.OutputCount).ToArray()
            : request.RecordSelection.Resolve(dataSet.Rows.Count);
        CultureInfo culture = string.IsNullOrWhiteSpace(request.CultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(request.CultureName);
        DateTimeOffset createdAt = clock.UtcNow;
        JobTimeContext jobTime = new()
        {
            ReferenceTime = request.ReferenceTime ?? clock.LocalNow,
            Culture = culture,
        };
        SerialPlan serialPlan = SerialPlanService.Create(request.Document, selectedIndices, copies.RecordCopies);
        PrintJobContext context = new()
        {
            CreatedAt = createdAt,
            JobTime = jobTime,
            SerialPlan = serialPlan,
        };
        List<LogicalPlacement> logical = [];
        foreach (int jobCopy in Enumerable.Range(0, copies.JobCopies))
        {
            foreach (int recordIndex in selectedIndices)
            {
                foreach (int recordCopy in Enumerable.Range(0, copies.RecordCopies))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DataRowSnapshot row = dataSet.Rows[recordIndex];
                    ResolvedDataRecord resolved = dataPreviewService.Resolve(
                        request.Document,
                        row,
                        jobTime,
                        serialPlan,
                        recordCopy);
                    logical.Add(new LogicalPlacement(recordIndex, recordCopy, jobCopy, resolved));
                }
            }
        }

        IReadOnlyList<PlannedPhysicalPage> pages = request.Document.Page.Layout switch
        {
            SinglePageLayout => PlanOnePerPage(request.Document, logical, OrientedMediaSize(request.Document.Page.Media)),
            RollLabelLayout roll => PlanOnePerPage(request.Document, logical, roll.LabelSize),
            SheetLabelLayout sheet => PlanSheet(request, logical, sheet),
            _ => throw new NotSupportedException($"Unsupported document layout: {request.Document.Page.Layout.GetType().Name}"),
        };
        return new PrintJobPlan
        {
            DocumentSnapshot = request.Document,
            Context = context,
            SelectedRecordIndices = selectedIndices.ToArray(),
            Pages = pages,
        };
    }

    private static PlannedPhysicalPage[] PlanOnePerPage(
        TemplateDocument document,
        IReadOnlyList<LogicalPlacement> logical,
        MmSize pageSize)
    {
        MmSize contentSize = ContentSize(document);
        return logical.Select((item, index) =>
        {
            LabelCell cell = new(1, 0, 0, new MmRect(0, 0, pageSize.Width, pageSize.Height));
            PlannedPlacement placement = CreatePlacement(item, index, index + 1, cell, contentSize);
            return new PlannedPhysicalPage
            {
                PageNumber = index + 1,
                Size = pageSize,
                Placements = [placement],
            };
        }).ToArray();
    }

    private static List<PlannedPhysicalPage> PlanSheet(
        PrintJobRequest request,
        IReadOnlyList<LogicalPlacement> logical,
        SheetLabelLayout sheet)
    {
        sheet.Validate();
        LabelCell[] traversal = CreateCells(sheet);
        int startingCell = request.StartingCellOverride ?? sheet.StartingCell;
        int startPosition = Array.FindIndex(traversal, cell => cell.Index == startingCell);
        if (startPosition < 0)
        {
            throw new InvalidOperationException("Starting label cell is invalid.");
        }

        MmSize pageSize = OrientedMediaSize(request.Document.Page.Media);
        List<PlannedPhysicalPage> pages = [];
        int logicalIndex = 0;
        int placementIndex = 0;
        int pageNumber = 1;
        while (logicalIndex < logical.Count)
        {
            IReadOnlyList<LabelCell> available = pageNumber == 1 ? traversal[startPosition..] : traversal;
            List<PlannedPlacement> placements = [];
            foreach (LabelCell cell in available)
            {
                if (logicalIndex >= logical.Count)
                {
                    break;
                }

                placements.Add(CreatePlacement(
                    logical[logicalIndex++],
                    placementIndex++,
                    pageNumber,
                    cell,
                    sheet.LabelSize));
            }

            pages.Add(new PlannedPhysicalPage
            {
                PageNumber = pageNumber++,
                Size = pageSize,
                Placements = placements,
            });
        }

        return pages;
    }

    private static PlannedPlacement CreatePlacement(
        LogicalPlacement logical,
        int placementIndex,
        int pageNumber,
        LabelCell cell,
        MmSize contentSize) =>
        new()
        {
            PlacementIndex = placementIndex,
            PageNumber = pageNumber,
            RecordIndex = logical.RecordIndex,
            RecordCopyIndex = logical.RecordCopyIndex,
            JobCopyIndex = logical.JobCopyIndex,
            Cell = cell,
            ContentSize = contentSize,
            Transform = RenderTransform.Translation(cell.Bounds.X, cell.Bounds.Y),
            ResolvedElements = logical.Resolved.Elements,
            Issues = logical.Resolved.Issues,
        };

    private static LabelCell[] CreateCells(SheetLabelLayout sheet)
    {
        List<LabelCell> physical = [];
        for (int row = 0; row < sheet.Rows; row++)
        {
            for (int column = 0; column < sheet.Columns; column++)
            {
                int index = row * sheet.Columns + column + 1;
                double x = sheet.Margins.Left + column * (sheet.LabelSize.Width + sheet.HorizontalGapMm);
                double y = sheet.Margins.Top + row * (sheet.LabelSize.Height + sheet.VerticalGapMm);
                physical.Add(new LabelCell(index, row, column, new MmRect(x, y, sheet.LabelSize.Width, sheet.LabelSize.Height)));
            }
        }

        return (sheet.TraversalOrder == TraversalOrder.RowMajor
                ? physical.OrderBy(static cell => cell.Row).ThenBy(static cell => cell.Column)
                : physical.OrderBy(static cell => cell.Column).ThenBy(static cell => cell.Row))
            .ToArray();
    }

    private static MmSize ContentSize(TemplateDocument document) => document.Page.Layout switch
    {
        RollLabelLayout roll => roll.LabelSize,
        SheetLabelLayout sheet => sheet.LabelSize,
        _ => OrientedMediaSize(document.Page.Media),
    };

    private static MmSize OrientedMediaSize(MediaDefinition media) =>
        media.Orientation == PageOrientation.Landscape
            ? new MmSize(media.Size.Height, media.Size.Width)
            : media.Size;

    private static DataSetSnapshot FixedDataSet(int outputCount) => new()
    {
        SourceDisplayName = "Fixed content",
        Schema = new DataSchema { Columns = Array.Empty<DataColumnDefinition>() },
        Rows = Enumerable.Range(0, outputCount)
            .Select(index => new DataRowSnapshot
            {
                Index = index,
                Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            })
            .ToArray(),
    };

    private sealed record LogicalPlacement(
        int RecordIndex,
        int RecordCopyIndex,
        int JobCopyIndex,
        ResolvedDataRecord Resolved);
}
