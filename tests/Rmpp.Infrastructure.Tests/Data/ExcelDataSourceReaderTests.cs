using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Rmpp.Application.Data;
using Rmpp.Infrastructure.Data;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Data;

public sealed class ExcelDataSourceReaderTests
{
    [Fact]
    public async Task ReadsSelectedWorksheetRangeCachedFormulaAndDateWithoutExecution()
    {
        string path = TemporaryPath("xlsm");
        try
        {
            CreateWorkbook(path);

            DataSetSnapshot result = await new ExcelDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = path,
                WorksheetName = "数据",
                CellRange = "A1:D3",
                CultureName = "zh-CN",
            });

            Assert.Equal(2, result.Rows.Count);
            Assert.Equal("红枫叶", result.Rows[0].GetValue("名称"));
            Assert.Equal("2", result.Rows[0].GetValue("公式缓存"));
            Assert.Contains("2026", result.Rows[0].GetValue("日期"), StringComparison.Ordinal);
            Assert.Contains(result.Warnings, warning => warning.Contains("formula", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Warnings, warning => warning.Contains("VBA", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task MissingWorksheetAndMalformedWorkbookFailSafely()
    {
        string workbook = TemporaryPath("xlsx");
        string malformed = TemporaryPath("xlsx");
        try
        {
            CreateWorkbook(workbook);
            await File.WriteAllTextAsync(malformed, "not a zip package");

            await Assert.ThrowsAsync<DataImportException>(() => new ExcelDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = workbook,
                WorksheetName = "不存在",
            }));
            await Assert.ThrowsAsync<DataImportException>(() => new ExcelDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = malformed,
            }));
        }
        finally
        {
            File.Delete(workbook);
            File.Delete(malformed);
        }
    }

    private static void CreateWorkbook(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.MacroEnabledWorkbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        WorkbookStylesPart stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = CreateStylesheet();
        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        SheetData data = new();
        data.Append(
            Row(1, TextCell("A1", "名称"), TextCell("B1", "金额"), TextCell("C1", "公式缓存"), TextCell("D1", "日期")),
            Row(2, TextCell("A2", "红枫叶"), NumberCell("B2", "12.5"), FormulaCell("C2", "1+1", "2"), DateCell("D2", new DateTime(2026, 7, 27))),
            Row(3, TextCell("A3", "标签"), NumberCell("B3", "8"), FormulaCell("C3", "2+2", "4"), DateCell("D3", new DateTime(2026, 7, 28))));
        worksheetPart.Worksheet = new Worksheet(data);
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "数据",
        });
        workbookPart.Workbook.Save();
    }

    private static Stylesheet CreateStylesheet() => new(
        new Fonts(new Font()),
        new Fills(new Fill()),
        new Borders(new Border()),
        new CellStyleFormats(new CellFormat()),
        new CellFormats(
            new CellFormat(),
            new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }));

    private static Row Row(uint index, params Cell[] cells) => new(cells) { RowIndex = index };

    private static Cell TextCell(string reference, string value) => new()
    {
        CellReference = reference,
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value)),
    };

    private static Cell NumberCell(string reference, string value) => new()
    {
        CellReference = reference,
        CellValue = new CellValue(value),
    };

    private static Cell FormulaCell(string reference, string formula, string cached) => new()
    {
        CellReference = reference,
        CellFormula = new CellFormula(formula),
        CellValue = new CellValue(cached),
    };

    private static Cell DateCell(string reference, DateTime value) => new()
    {
        CellReference = reference,
        StyleIndex = 1,
        CellValue = new CellValue(value.ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture)),
    };

    private static string TemporaryPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"rmpp-excel-{Guid.NewGuid():N}.{extension}");
}
