using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Rmpp.Application.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Infrastructure.Data;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Infrastructure.Templates;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Performance;

public sealed class PackageDataPdfPerformanceTests
{
    private static readonly string[] CsvHeader = ["id,name"];

    [Fact, Trait("Category", "Performance")]
    public async Task OpensPackageWithTwoThousandElementsWithinThreshold()
    {
        TemplateDocument original = TemplateDocument.CreateNew("大型模板");
        Guid layer = original.Layers[0].Id;
        original = original with
        {
            Elements = Enumerable.Range(0, 2_000).Select(index => (TemplateElement)new RectangleElement
            {
                Name = "R" + index,
                LayerId = layer,
                Bounds = new MmRect(index % 100, index / 100, 1, 1),
            }).ToArray(),
        };
        await using MemoryStream package = new();
        await new RmppPackageWriter().WriteAsync(package, new TemplatePackageContent { Document = original });
        package.Position = 0;

        Stopwatch watch = Stopwatch.StartNew();
        TemplatePackageContent opened = await new RmppPackageReader().ReadAsync(package);
        watch.Stop();

        Assert.Equal(2_000, opened.Document.Elements.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"打开耗时 {watch.Elapsed}");
    }

    [Fact, Trait("Category", "Performance")]
    public async Task ImportsTwentyThousandCsvRowsWithinThreshold()
    {
        string path = Path.Combine(Path.GetTempPath(), "rmpp-perf-" + Guid.NewGuid().ToString("N") + ".csv");
        await File.WriteAllLinesAsync(path, CsvHeader.Concat(Enumerable.Range(0, 20_000).Select(index => $"{index},名称{index}")));
        try
        {
            Stopwatch watch = Stopwatch.StartNew();
            DataSetSnapshot data = await new CsvDataSourceReader().ReadAsync(new DataImportOptions { FilePath = path, MaximumRows = 25_000 });
            watch.Stop();

            Assert.Equal(20_000, data.Count);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), $"CSV 导入耗时 {watch.Elapsed}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact, Trait("Category", "Performance")]
    public async Task ImportsTwentyThousandXlsxRowsWithinThreshold()
    {
        string path = Path.Combine(Path.GetTempPath(), "rmpp-perf-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            CreateLargeWorkbook(path, 20_000);

            Stopwatch watch = Stopwatch.StartNew();
            DataSetSnapshot data = await new ExcelDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = path,
                WorksheetName = "数据",
                MaximumRows = 25_000,
            });
            watch.Stop();

            Assert.Equal(20_000, data.Count);
            Assert.Equal("红枫叶19999", data.Rows[^1].GetValue("名称"));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"XLSX 导入耗时 {watch.Elapsed}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact, Trait("Category", "Performance")]
    public async Task ExportsFiftyPdfPagesWithinThreshold()
    {
        RenderPage[] pages = Enumerable.Range(1, 50).Select(number => new RenderPage
        {
            PageNumber = number,
            Size = new MmSize(100, 100),
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, 100, 100)),
            Commands = Array.Empty<RenderCommand>(),
        }).ToArray();
        await using MemoryStream output = new();

        Stopwatch watch = Stopwatch.StartNew();
        await new LocalRenderExporter().ExportAsync(new RenderScene { DocumentId = Guid.NewGuid(), Pages = pages }, output, new Rmpp.Application.Abstractions.RenderExportOptions());
        watch.Stop();

        Assert.True(output.Length > 100);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"PDF 导出耗时 {watch.Elapsed}");
    }

    private static void CreateLargeWorkbook(string path, int rowCount)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        SheetData sheetData = new();
        sheetData.Append(CreateRow(1, TextCell("A1", "编号"), TextCell("B1", "名称"), TextCell("C1", "数量")));

        for (int index = 0; index < rowCount; index++)
        {
            uint rowNumber = (uint)index + 2;
            sheetData.Append(CreateRow(
                rowNumber,
                NumberCell($"A{rowNumber}", index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                TextCell($"B{rowNumber}", $"红枫叶{index}"),
                NumberCell($"C{rowNumber}", (index % 100).ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        worksheetPart.Worksheet = new Worksheet(sheetData);
        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "数据",
        });
        workbookPart.Workbook.Save();
    }

    private static Row CreateRow(uint index, params Cell[] cells) => new(cells) { RowIndex = index };

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
}
