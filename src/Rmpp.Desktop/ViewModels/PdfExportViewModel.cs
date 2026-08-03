using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Printing;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Rmpp.Infrastructure.Pdf;

namespace Rmpp.Desktop.ViewModels;

/// <summary>把同一个打印计划按页流式组装并交给本地 PDF exporter。</summary>
public sealed partial class PdfExportViewModel : ObservableObject, IDisposable
{
    private readonly IRenderExporter exporter;
    private readonly PrintPreviewService previewService;
    private readonly IRenderAssetProvider? assetProvider;
    private CancellationTokenSource? cancellation;
    private PrintJobPlan? plan;

    public PdfExportViewModel(
        IRenderExporter exporter,
        PrintPreviewService? previewService = null,
        IRenderAssetProvider? assetProvider = null)
    {
        this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        this.previewService = previewService ?? new PrintPreviewService();
        this.assetProvider = assetProvider;
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => plan is not null && !IsBusy && !string.IsNullOrWhiteSpace(OutputPath));
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => IsBusy);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private string outputPath = string.Empty;

    [ObservableProperty]
    private int firstPage = 1;

    [ObservableProperty]
    private int lastPage = 1;

    [ObservableProperty]
    private bool includePrintableBackgrounds = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    [ObservableProperty]
    private int completedPages;

    [ObservableProperty]
    private string statusText = string.Empty;

    public IAsyncRelayCommand ExportCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public int TotalPages => plan?.Pages.Count ?? 0;
    public string PageRangeExplanation => plan is null
        ? "请先生成打印计划。"
        : $"这里是计划生成后的物理页范围，共 {plan.Pages.Count} 页；上方记录范围或输出数量已经包含在计划中。";
    public double FirstPageValue { get => FirstPage; set => FirstPage = ToPositiveInt(value, FirstPage); }
    public double LastPageValue { get => LastPage; set => LastPage = ToPositiveInt(value, LastPage); }

    partial void OnFirstPageChanged(int value) => OnPropertyChanged(nameof(FirstPageValue));
    partial void OnLastPageChanged(int value) => OnPropertyChanged(nameof(LastPageValue));

    public void Load(PrintJobPlan jobPlan)
    {
        plan = jobPlan ?? throw new ArgumentNullException(nameof(jobPlan));
        FirstPage = 1;
        LastPage = Math.Max(1, plan.Pages.Count);
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageRangeExplanation));
        ExportCommand.NotifyCanExecuteChanged();
    }

    private async Task ExportAsync()
    {
        if (plan is null) return;
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        IsBusy = true;
        CompletedPages = 0;
        StatusText = "正在生成 PDF…";
        try
        {
            int firstPage = Math.Clamp(FirstPage, 1, plan.Pages.Count);
            int lastPage = Math.Clamp(LastPage, firstPage, plan.Pages.Count);
            List<RenderPage> pages = [];
            for (int index = firstPage - 1; index < lastPage; index++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                RenderScene pageScene = await previewService.GetPageAsync(plan, index, RenderTarget.Pdf, cancellation.Token).ConfigureAwait(true);
                RenderPage page = pageScene.Pages[0];
                if (!IncludePrintableBackgrounds)
                {
                    HashSet<Guid> backgroundIds = plan.DocumentSnapshot.Backgrounds.Select(static background => background.Id).ToHashSet();
                    page = page with { Commands = page.Commands.Where(command => !backgroundIds.Contains(command.SourceId)).ToArray() };
                }
                pages.Add(page);
                CompletedPages = pages.Count;
            }
            RenderScene scene = new()
            {
                DocumentId = plan.DocumentSnapshot.Id,
                Pages = pages,
                Issues = pages.SelectMany(static page => page.Issues).ToArray(),
            };
            string? directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string fullPath = Path.GetFullPath(OutputPath);
            string outputDirectory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("PDF 输出路径没有有效目录。");
            string temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (FileStream stream = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await exporter.ExportAsync(scene, stream, new RenderExportOptions
                    {
                        AssetProvider = assetProvider,
                        ImageSourceDpi = 300,
                    }, cancellation.Token).ConfigureAwait(true);
                    await stream.FlushAsync(cancellation.Token).ConfigureAwait(true);
                }

                await PdfDocumentVerifier.VerifyAsync(temporaryPath, pages.Count, cancellation.Token).ConfigureAwait(true);
                ReplaceVerifiedPdf(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            StatusText = $"PDF 已保存：{OutputPath}";
        }
        catch (OperationCanceledException) { StatusText = "已取消 PDF 导出。"; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            StatusText = exception.Message;
        }
        finally
        {
            IsBusy = false;
            cancellation?.Dispose();
            cancellation = null;
        }
    }

    /// <summary>临时 PDF 已完成结构回读后才替换目标，避免失败或取消留下不可打开的文件。</summary>
    private static void ReplaceVerifiedPdf(string temporaryPath, string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            File.Move(temporaryPath, destinationPath);
            return;
        }

        string backupPath = destinationPath + ".bak";
        try
        {
            File.Replace(temporaryPath, destinationPath, backupPath, ignoreMetadataErrors: true);
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
        catch
        {
            if (File.Exists(backupPath) && !File.Exists(destinationPath))
            {
                File.Move(backupPath, destinationPath);
            }
            throw;
        }
    }

    private static int ToPositiveInt(double value, int fallback) =>
        double.IsFinite(value) && value >= 1 && value <= int.MaxValue ? checked((int)value) : fallback;

    public void Dispose()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }
}
