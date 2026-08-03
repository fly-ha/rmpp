using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Desktop.ViewModels;
using Rmpp.Desktop.Views;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Data;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Scene;
using Rmpp.Domain.Printing;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Domain.Geometry;
using System.IO;
using Rmpp.Infrastructure.Pdf;
using Xunit;

namespace Rmpp.Desktop.Tests.ViewModels;

public sealed class Phase12ViewModelTests
{
    [Fact]
    public async Task DataImportRemainsInSessionAndPopulatesPreviewFields()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        await File.WriteAllTextAsync(path, "name\n张三");
        try
        {
            using DataImportViewModel importer = new([new FakeReader()]);
            importer.SetFilePath(path);
            DataSetSnapshot? imported = null;
            importer.Imported += (_, snapshot) => imported = snapshot;

            await importer.ImportCommand.ExecuteAsync(null);

            Assert.NotNull(imported);
            Assert.Equal("张三", imported.Rows[0].GetValue("name"));
            using DocumentTabViewModel tab = new(TemplateDocument.CreateNew("数据测试"));
            tab.AttachDataSet(imported);
            Assert.Same(imported, tab.DataSet);
            Assert.Equal("name", Assert.Single(tab.ExpressionEditor.Fields));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExpressionEditorUsesRestrictedParserAndShowsSampleResult()
    {
        ExpressionEditorViewModel editor = new();
        editor.SetFields(["订单号"]);
        editor.Source = "'NO-' + [订单号]";
        editor.ValidateCommand.Execute(null);
        editor.SetSampleValues(new Dictionary<string, string?> { ["订单号"] = "1001" });
        Assert.False(editor.HasError);
        Assert.Equal("NO-1001", editor.SampleResult);

        editor.Source = "file('secret.txt')";
        editor.ValidateCommand.Execute(null);
        Assert.True(editor.HasError);
    }

    [Fact]
    public void PrintSetupFreezesOnePlanForPreviewAndPdf()
    {
        PrintSetupViewModel setup = new(renderExporter: new FakeExporter());
        setup.Load(TemplateDocument.CreateNew("计划测试"), null);
        setup.BuildPlanCommand.Execute(null);

        Assert.NotNull(setup.Plan);
        Assert.Same(setup.Plan, setup.Preview!.Plan);
        Assert.NotNull(setup.PdfExport);
        Assert.Equal(setup.Plan!.Pages.Count, setup.PdfExport!.LastPage);
    }

    [Fact]
    public async Task PreviewDoesNotRenderUntilExplicitlyRequested()
    {
        PrintPreviewService service = new();
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest
        {
            Document = TemplateDocument.CreateNew("延迟预览"),
        });
        using PrintPreviewViewModel preview = new(service);

        preview.Load(plan);

        Assert.Equal(0, service.BuiltPageCount);
        await preview.LoadPageAsync();
        Assert.Equal(1, service.BuiltPageCount);
        Assert.NotNull(preview.PageImage);
    }

    [Fact]
    public async Task PrintSetupDefersPrinterDiscoveryAndRestoresTemplateDefaults()
    {
        CountingPrintSystem printSystem = new();
        WindowsPrinterCatalog catalog = new(printSystem);
        TemplateDocument document = TemplateDocument.CreateNew("打印默认值") with
        {
            PrintSettings = new TemplatePrintSettings
            {
                OutputCount = 100,
                Copies = 2,
                CopyOrder = PrintCopyOrder.Collated,
            },
        };
        PrintSetupViewModel setup = new(printerCatalog: catalog);

        Assert.Equal(0, printSystem.CallCount);
        setup.Load(document, null);
        Assert.Equal(0, printSystem.CallCount);
        Assert.Equal(100, setup.OutputCount);
        Assert.Equal(2, setup.Copies);
        Assert.Equal(PrintCopyOrder.Collated, setup.CopyOrder);

        await setup.RefreshPrintersCommand.ExecuteAsync(null);
        Assert.Equal(1, printSystem.CallCount);
        Assert.NotEmpty(setup.Media);
    }

    [Fact]
    public async Task PreferredPrinterIsRestoredAndSavedAsAnOptionalTemplatePreference()
    {
        CountingPrintSystem printSystem = new();
        WindowsPrintQueueSnapshot queue = Assert.Single(printSystem.GetQueues());
        string preferredId = WindowsPrinterIdentityResolver.Resolve(queue).StableId;
        TemplateDocument document = TemplateDocument.CreateNew("首选打印机") with
        {
            PrintSettings = new TemplatePrintSettings
            {
                PreferredPrinterId = preferredId,
                PreferredPrinterDisplayName = queue.DisplayName,
            },
        };
        TemplatePrintSettings? saved = null;
        PrintSetupViewModel setup = new(
            printerCatalog: new WindowsPrinterCatalog(printSystem),
            persistPrintSettings: settings => saved = settings);
        setup.Load(document, null);

        await setup.RefreshPrintersCommand.ExecuteAsync(null);
        setup.SaveTemplatePrintSettingsCommand.Execute(null);

        Assert.Equal(preferredId, setup.SelectedPrinter?.Identity.StableId);
        Assert.Equal(preferredId, saved?.PreferredPrinterId);
        Assert.Equal(queue.DisplayName, saved?.PreferredPrinterDisplayName);
    }

    [Fact]
    public void ImportedDataDefaultsToAllRowsAndRangeCannotExceedTheDataSet()
    {
        DataSetSnapshot data = new()
        {
            SourceDisplayName = "三条记录",
            Schema = new DataSchema { Columns = [new DataColumnDefinition("name", 0)] },
            Rows = Enumerable.Range(1, 3).Select(index => new DataRowSnapshot
            {
                Index = index - 1,
                Values = new Dictionary<string, string?> { ["name"] = $"记录{index}" },
            }).ToArray(),
        };
        PrintSetupViewModel setup = new();

        setup.Load(TemplateDocument.CreateNew("数据范围"), data);
        setup.FirstRecord = 99;
        setup.LastRecord = 99;

        Assert.True(setup.HasDataSource);
        Assert.Equal(3, setup.MaximumRecord);
        Assert.Equal(3, setup.FirstRecord);
        Assert.Equal(3, setup.LastRecord);
    }

    [Fact]
    public async Task NoDataSerialOutputCreatesMultiplePreviewAndVerifiedPdfPages()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rmpp-serial-{Guid.NewGuid():N}.pdf");
        try
        {
            TemplateDocument document = TemplateDocument.CreateNew("流水号多页");
            SerialElement serial = new()
            {
                LayerId = document.Layers[0].Id,
                Bounds = new MmRect(10, 10, 50, 15),
                Definition = new SerialDefinition { Start = 100, Step = 2 },
            };
            document = document with { Elements = [serial] };
            PrintSetupViewModel setup = new(renderExporter: new LocalRenderExporter());
            setup.Load(document, null);
            setup.OutputCount = 3;

            setup.BuildPlanCommand.Execute(null);

            Assert.Equal(3, setup.Plan?.Pages.Count);
            string?[] expectedSerials = ["100", "102", "104"];
            Assert.Equal(expectedSerials, setup.Plan!.Pages
                .Select(page => Assert.Single(Assert.Single(page.Placements).ResolvedElements).Text)
                .ToArray());
            Assert.Equal(1, setup.PdfExport?.FirstPage);
            Assert.Equal(3, setup.PdfExport?.LastPage);
            setup.PdfExport!.OutputPath = path;
            await setup.PdfExport.ExportCommand.ExecuteAsync(null);

            Assert.True(File.Exists(path));
            await PdfDocumentVerifier.VerifyAsync(path, 3);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void StartingLabelPositionIsOnlyAvailableForSheetLayouts()
    {
        PrintSetupViewModel single = new();
        single.Load(TemplateDocument.CreateNew("单页"), null);
        Assert.False(single.HasSheetLabelLayout);
        Assert.Null(single.StartingCell);

        TemplateDocument sheetDocument = TemplateDocument.CreateNew("标签纸");
        sheetDocument = sheetDocument with
        {
            Page = sheetDocument.Page with
            {
                Layout = new SheetLabelLayout
                {
                    LabelSize = new MmSize(50, 30),
                    Margins = new MmThickness(0),
                    Rows = 2,
                    Columns = 3,
                    StartingCell = 2,
                },
            },
        };
        PrintSetupViewModel sheet = new();
        sheet.Load(sheetDocument, null);

        Assert.True(sheet.HasSheetLabelLayout);
        Assert.Equal(6, sheet.MaximumStartingCell);
        Assert.Equal(2, sheet.StartingCell);
    }

    [Fact]
    public void Phase12WindowsAndLocalResourcesConstructOnStaThread()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                _ = new DataImportDialog();
                _ = new PrintSetupDialog();
                _ = new CalibrationWizard();
                _ = new RecoveryDialog();
                _ = new SettingsCatalogDialog();
                _ = new DataPreviewPanel();
                _ = new ExpressionEditorView();
                _ = new PrintPreviewView();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
    }

    private sealed class FakeReader : IDataSourceReader
    {
        public bool CanRead(string fileExtension) => fileExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
        public Task<DataSetSnapshot> ReadAsync(DataImportOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new DataSetSnapshot
        {
            SourceDisplayName = Path.GetFileName(options.FilePath),
            Schema = new DataSchema { Columns = [new DataColumnDefinition("name", 0)] },
            Rows = [new DataRowSnapshot { Index = 0, Values = new Dictionary<string, string?> { ["name"] = "张三" } }],
        });
        public async IAsyncEnumerable<DataRowSnapshot> PreviewAsync(DataImportOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return (await ReadAsync(options, cancellationToken)).Rows[0];
        }
    }

    private sealed class FakeExporter : IRenderExporter
    {
        public Task ExportAsync(RenderScene scene, Stream destination, RenderExportOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CountingPrintSystem : IWindowsPrintSystemAdapter
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<WindowsPrintQueueSnapshot> GetQueues(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return
            [
                new WindowsPrintQueueSnapshot
                {
                    ServerName = "local",
                    QueueName = "test",
                    DisplayName = "测试打印机",
                    DriverName = "test",
                    PortName = "PORT1",
                    IsDefault = true,
                    Media =
                    [
                        new WindowsMediaDefinition
                        {
                            Key = "a4",
                            DisplayName = "A4",
                            DriverName = "ISOA4",
                            Size = new MmSize(210, 297),
                        },
                    ],
                    Orientations = new HashSet<PrintMediaOrientation> { PrintMediaOrientation.Portrait },
                    Resolutions = [new WindowsPrintResolution(300, 300)],
                },
            ];
        }
    }
}
