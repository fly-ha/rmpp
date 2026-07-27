using System.Text.Json;
using System.IO;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Desktop.Composition;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Data;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;
using Rmpp.Infrastructure.Templates;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Desktop.Services;

/// <summary>供发布脚本调用的无界面离线冒烟工作流；只在显式命令行模式下运行。</summary>
public static class OfflineSmokeWorkflow
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = true };
    public static async Task<string> RunAsync(string root, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string fullRoot = Path.GetFullPath(root);
        Directory.CreateDirectory(fullRoot);
        string files = Path.Combine(fullRoot, "files");
        Directory.CreateDirectory(files);

        SqliteAppDatabase database = new(new DatabaseOptions
        {
            StorageMode = DatabaseStorageMode.Installed,
            InstalledDataRoot = fullRoot,
            ApplicationDirectoryName = "state",
        });
        await database.InitializeAsync(cancellationToken).ConfigureAwait(false);
        SettingsRepository settings = new(database);
        SettingDefinition<bool> completedSetting = new("offline.smoke.completed", false, 1);
        await settings.SetAsync(completedSetting, true, cancellationToken).ConfigureAwait(false);

        TemplatePackageContent source = new() { Document = TemplateDocument.CreateNew("离线冒烟模板") };
        string templatePath = Path.Combine(files, "smoke.rmpp");
        await new AtomicTemplateFileWriter().WriteAsync(templatePath, source, cancellationToken).ConfigureAwait(false);
        await using FileStream templateInput = new(templatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        TemplatePackageContent opened = await new RmppPackageReader().ReadAsync(templateInput, cancellationToken).ConfigureAwait(false);

        string csvPath = Path.Combine(files, "smoke.csv");
        await File.WriteAllTextAsync(csvPath, "name,code\n红枫叶,RMPP-001\n定位打印,RMPP-002", cancellationToken).ConfigureAwait(false);
        DataSetSnapshot data = await new CsvDataSourceReader().ReadAsync(new DataImportOptions { FilePath = csvPath }, cancellationToken).ConfigureAwait(false);
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest { Document = opened.Document, DataSet = data }, cancellationToken);
        PrintPreviewService preview = new();
        List<RenderPage> pages = [];
        for (int index = 0; index < plan.Pages.Count; index++)
        {
            RenderScene scene = await preview.GetPageAsync(plan, index, RenderTarget.Pdf, cancellationToken).ConfigureAwait(false);
            pages.Add(scene.Pages[0]);
        }
        string pdfPath = Path.Combine(files, "smoke.pdf");
        await using (FileStream pdf = new(pdfPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await new LocalRenderExporter().ExportAsync(new RenderScene { DocumentId = opened.Document.Id, Pages = pages }, pdf, new Rmpp.Application.Abstractions.RenderExportOptions(), cancellationToken).ConfigureAwait(false);
        }

        AppStoragePaths paths = new(Path.Combine(fullRoot, "app"), true);
        Directory.CreateDirectory(paths.RecoveryDirectory);
        using RecoveryCoordinator recovery = new(paths, new RecoverySessionRepository(database), new AtomicTemplateFileWriter(), new RmppPackageReader());
        await recovery.SaveAsync(source, templatePath, cancellationToken).ConfigureAwait(false);

        string helpRoot = Path.Combine(fullRoot, "help");
        Directory.CreateDirectory(helpRoot);
        await File.WriteAllTextAsync(Path.Combine(helpRoot, "start.md"), "# 离线帮助\n本地内容", cancellationToken).ConfigureAwait(false);
        string helpText = new LocalHelpService(helpRoot).ReadTopic("start");
        SettingReadResult<bool> completed = await settings.GetAsync(completedSetting, cancellationToken).ConfigureAwait(false);
        string reportPath = Path.Combine(fullRoot, "offline-smoke-report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            TemplateOpened = opened.Document.Id == source.Document.Id,
            ImportedRows = data.Count,
            PlannedPages = plan.Pages.Count,
            PdfBytes = new FileInfo(pdfPath).Length,
            RecoveryCount = (await recovery.GetAvailableAsync(cancellationToken).ConfigureAwait(false)).Count,
            HelpLoaded = helpText.Contains("本地内容", StringComparison.Ordinal),
            SettingStored = !completed.UsedDefault && completed.Value,
        }, ReportJsonOptions), cancellationToken).ConfigureAwait(false);
        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        return reportPath;
    }
}
