using System.Diagnostics;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Xunit;

namespace Rmpp.Application.Tests.Performance;

public sealed class PrintPlanningPerformanceTests
{
    [Fact, Trait("Category", "Performance")]
    public void PlansFiveThousandSheetLabelsWithinRegressionThreshold()
    {
        TemplateDocument document = TemplateDocument.CreateNew("性能") with
        {
            Page = new PageDefinition
            {
                Media = new MediaDefinition("A4", new MmSize(210, 297)),
                Layout = new SheetLabelLayout
                {
                    LabelSize = new MmSize(40, 25),
                    Margins = new MmThickness(5),
                    Rows = 10,
                    Columns = 5,
                    HorizontalGapMm = 1,
                    VerticalGapMm = 1,
                },
            },
        };
        DataSetSnapshot data = new()
        {
            SourceDisplayName = "memory",
            Schema = new DataSchema { Columns = Array.Empty<DataColumnDefinition>() },
            Rows = Enumerable.Range(0, 5_000).Select(index => new DataRowSnapshot
            {
                Index = index,
                Values = new Dictionary<string, string?>(),
            }).ToArray(),
        };
        Stopwatch watch = Stopwatch.StartNew();
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest { Document = document, DataSet = data });
        watch.Stop();
        Assert.Equal(5_000, plan.TotalPlacements);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), $"规划耗时 {watch.Elapsed}");
    }
}
