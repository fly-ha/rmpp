using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Desktop.ViewModels;
using Rmpp.Desktop.Views;
using Rmpp.Domain.Documents;
using Rmpp.Rendering.Scene;
using System.IO;
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
}
