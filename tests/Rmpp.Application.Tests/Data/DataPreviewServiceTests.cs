using System.Globalization;
using Rmpp.Application.Data;
using Rmpp.Application.Validation;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Application.Tests.Data;

public sealed class DataPreviewServiceTests
{
    [Fact]
    public void BindingValidationFindsMissingAndUnusedFields()
    {
        TextElement text = new()
        {
            Bounds = new MmRect(0, 0, 40, 10),
            Content = new ElementExpression("=concat([编号], [不存在])"),
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(text);
        DataSchema schema = new()
        {
            Columns =
            [
                new DataColumnDefinition("编号", 0),
                new DataColumnDefinition("未使用", 1),
            ],
        };

        BindingValidationResult result = new BindingValidationService().Validate(document, schema);

        Assert.Contains("编号", result.ReferencedFields);
        Assert.Contains("未使用", result.UnusedFields);
        ValidationIssue missing = Assert.Single(result.Issues, issue => issue.Code == "missing-data-field");
        Assert.Equal(text.Id, missing.Location.ElementId);
    }

    [Fact]
    public void ResolveCombinesFixedFieldsSerialAndFrozenJobTime()
    {
        TextElement text = new()
        {
            Bounds = new MmRect(0, 0, 40, 10),
            Content = new ElementExpression("='订单-' + [编号]"),
        };
        SerialElement serial = new()
        {
            Bounds = new MmRect(0, 20, 30, 10),
            Definition = new SerialDefinition { Start = 10, Step = 2, Prefix = "S", MinimumDigits = 3 },
        };
        DateTimeElement date = new()
        {
            Bounds = new MmRect(0, 40, 40, 10),
            Definition = new DateTimeDefinition { Format = "yyyy-MM-dd HH:mm", CultureName = "zh-CN" },
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(text, serial, date);
        DataRowSnapshot row = new()
        {
            Index = 1,
            Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["编号"] = "A-2" },
        };
        JobTimeContext jobTime = new()
        {
            ReferenceTime = new DateTimeOffset(2026, 7, 27, 23, 59, 0, TimeSpan.FromHours(8)),
            Culture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        SerialPlan serialPlan = SerialPlanService.Create(document, recordCount: 3);

        Rmpp.Application.Data.ResolvedDataRecord result = new DataPreviewService().Resolve(
            document,
            row,
            jobTime,
            serialPlan);

        Assert.Empty(result.Issues);
        Assert.Equal("订单-A-2", result.Elements.Single(item => item.ElementId == text.Id).Text);
        Assert.Equal("S012", result.Elements.Single(item => item.ElementId == serial.Id).Text);
        Assert.Equal("2026-07-27 23:59", result.Elements.Single(item => item.ElementId == date.Id).Text);
    }

    [Fact]
    public void SerialPlanHonorsPerRecordAndPerCopyModes()
    {
        SerialElement perRecord = new()
        {
            Bounds = new MmRect(0, 0, 10, 10),
            Definition = new SerialDefinition { Start = 1, Step = 1, AdvancePerCopy = false },
        };
        SerialElement perCopy = new()
        {
            Bounds = new MmRect(0, 20, 10, 10),
            Definition = new SerialDefinition { Start = 100, Step = -2, AdvancePerCopy = true },
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(perRecord, perCopy);

        SerialPlan plan = SerialPlanService.Create(document, recordCount: 2, copiesPerRecord: 2);

        Assert.Equal("1", plan.Find(perRecord.Id, 0, 0)?.Text);
        Assert.Equal("1", plan.Find(perRecord.Id, 0, 1)?.Text);
        Assert.Equal("100", plan.Find(perCopy.Id, 0, 0)?.Text);
        Assert.Equal("98", plan.Find(perCopy.Id, 0, 1)?.Text);
        Assert.Equal("96", plan.Find(perCopy.Id, 1, 0)?.Text);
    }

    [Fact]
    public void RangeAndSearchNavigateSnapshotWithoutMutatingIt()
    {
        DataSetSnapshot snapshot = new()
        {
            SourceDisplayName = "orders.csv",
            Schema = new DataSchema { Columns = [new DataColumnDefinition("Name", 0)] },
            Rows = Enumerable.Range(0, 5).Select(index => new DataRowSnapshot
            {
                Index = index,
                Values = new Dictionary<string, string?> { ["Name"] = index == 3 ? "红枫叶" : $"Row {index}" },
            }).ToArray(),
        };

        Assert.Equal([1, 2], DataPreviewService.Range(snapshot, 1, 2).Select(static row => row.Index));
        Assert.Equal(3, DataPreviewService.FindNext(snapshot, "枫叶"));
        Assert.Equal(5, snapshot.Count);
    }
}
