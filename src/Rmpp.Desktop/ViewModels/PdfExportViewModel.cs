using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Printing;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Desktop.ViewModels;

/// <summary>把同一个打印计划按页流式组装并交给本地 PDF exporter。</summary>
public sealed partial class PdfExportViewModel : ObservableObject, IDisposable
{
    private readonly IRenderExporter exporter;
    private readonly PrintPreviewService previewService;
    private CancellationTokenSource? cancellation;
    private PrintJobPlan? plan;

    public PdfExportViewModel(IRenderExporter exporter, PrintPreviewService? previewService = null)
    {
        this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        this.previewService = previewService ?? new PrintPreviewService();
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

    public void Load(PrintJobPlan jobPlan)
    {
        plan = jobPlan ?? throw new ArgumentNullException(nameof(jobPlan));
        FirstPage = 1;
        LastPage = Math.Max(1, plan.Pages.Count);
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
            List<RenderPage> pages = [];
            for (int index = 0; index < plan.Pages.Count; index++)
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
                CompletedPages = index + 1;
            }
            RenderScene scene = new()
            {
                DocumentId = plan.DocumentSnapshot.Id,
                Pages = pages,
                Issues = pages.SelectMany(static page => page.Issues).ToArray(),
            };
            string? directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            await using FileStream stream = new(OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await exporter.ExportAsync(scene, stream, new RenderExportOptions
            {
                FirstPage = FirstPage,
                LastPage = LastPage,
            }, cancellation.Token).ConfigureAwait(true);
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

    public void Dispose()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }
}
