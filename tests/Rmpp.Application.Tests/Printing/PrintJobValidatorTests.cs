using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Application.Validation;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Domain.Styles;
using Xunit;

namespace Rmpp.Application.Tests.Printing;

public sealed class PrintJobValidatorTests
{
    [Fact]
    public void ReportsLayoutOverflowAndElementOutsideLabel()
    {
        RectangleElement element = TestDocumentFactory.Rectangle(x: 25, y: 15, width: 20, height: 10);
        (TemplateDocument source, _) = TestDocumentFactory.Create(element);
        TemplateDocument document = source with
        {
            Page = source.Page with
            {
                Media = new MediaDefinition("Small", new MmSize(60, 40)),
                Layout = new SheetLabelLayout
                {
                    LabelSize = new MmSize(30, 20),
                    Margins = new MmThickness(5),
                    Rows = 2,
                    Columns = 2,
                    HorizontalGapMm = 2,
                    VerticalGapMm = 2,
                },
            },
        };

        IReadOnlyList<ValidationIssue> issues = LayoutValidationService.Validate(new PrintJobRequest { Document = document });

        Assert.Contains(issues, issue => issue.Code == "sheet-layout-overflow");
        Assert.Contains(issues, issue => issue.Code == "element-outside-content" && issue.Location.ElementId == element.Id);
    }

    [Fact]
    public void OutputValidationLocatesRecordResourcesFontsBarcodeAndHardMargin()
    {
        TextElement text = new()
        {
            Bounds = new MmRect(0, 0, 3, 2),
            Content = new ElementExpression("=[Missing]"),
            TextStyle = new TextStyle { FontFamily = "RMPP Missing Font 4488", FontSizePoints = 24, Wrap = false },
        };
        BarcodeElement barcode = new()
        {
            Bounds = new MmRect(0, 10, 30, 10),
            Symbology = BarcodeSymbology.Ean13,
            Content = new ElementExpression("=[Code]"),
        };
        ImageElement packagedImage = new()
        {
            Bounds = new MmRect(0, 25, 10, 10),
            AssetId = Guid.NewGuid(),
        };
        ImageElement variableImage = new()
        {
            Bounds = new MmRect(0, 40, 10, 10),
            VariablePath = new ElementExpression("=[Path]"),
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(text, barcode, packagedImage, variableImage);
        DataSetSnapshot data = new()
        {
            SourceDisplayName = "bad.csv",
            Schema = new DataSchema
            {
                Columns = [new DataColumnDefinition("Code", 0), new DataColumnDefinition("Path", 1)],
            },
            Rows =
            [
                new DataRowSnapshot
                {
                    Index = 0,
                    Values = new Dictionary<string, string?>
                    {
                        ["Code"] = "ABC",
                        ["Path"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png"),
                    },
                },
            ],
        };
        PrintJobRequest request = new() { Document = document, DataSet = data };
        PrintJobPlan plan = new PrintJobPlanner().Plan(request);

        PrintJobValidationResult result = new PrintJobValidator().Validate(
            request,
            plan,
            printableArea: new MmRect(5, 5, 200, 287));

        Assert.True(result.HasBlockingErrors);
        Assert.Contains(result.Issues, issue => issue.Code == "missing-data-field" && issue.Location.ElementId == text.Id);
        Assert.Contains(result.Issues, issue => issue.Code == "missing-asset-reference" && issue.Location.ElementId == packagedImage.Id);
        Assert.Contains(result.Issues, issue => issue.Code == "missing-variable-image" && issue.Location.RecordIndex == 0);
        Assert.Contains(result.Issues, issue => issue.Code == "missing-font" && issue.Location.ElementId == text.Id);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-retail-barcode" && issue.Location.ElementId == barcode.Id);
        Assert.Contains(result.Issues, issue => issue.Code == "printable-area-intrusion" && issue.Location.PageNumber == 1);
    }
}
