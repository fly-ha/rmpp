using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rmpp.Application.Printing;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using SkiaSharp;

namespace Rmpp.Desktop.ViewModels;

/// <summary>按需渲染 immutable 打印计划中的当前物理页，并保持有界缓存。</summary>
public sealed partial class PrintPreviewViewModel : ObservableObject, IDisposable
{
    private readonly PrintPreviewService previewService;
    private readonly SkiaBitmapRenderer bitmapRenderer;
    private readonly IRenderAssetProvider? assetProvider;
    private PrintJobPlan? plan;
    private CancellationTokenSource? pageCancellation;
    private int pageLoadVersion;

    public PrintPreviewViewModel(
        PrintPreviewService? previewService = null,
        SkiaBitmapRenderer? bitmapRenderer = null,
        IRenderAssetProvider? assetProvider = null)
    {
        this.previewService = previewService ?? new PrintPreviewService();
        this.bitmapRenderer = bitmapRenderer ?? new SkiaBitmapRenderer();
        this.assetProvider = assetProvider;
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, () => PageIndex > 0);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => plan is not null && PageIndex < plan.Pages.Count - 1);
    }

    [ObservableProperty]
    private int pageIndex;

    [ObservableProperty]
    private RenderScene? scene;

    [ObservableProperty]
    private ImageSource? pageImage;

    [ObservableProperty]
    private string statusText = string.Empty;

    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public PrintJobPlan? Plan => plan;

    public void Load(PrintJobPlan jobPlan)
    {
        plan = jobPlan ?? throw new ArgumentNullException(nameof(jobPlan));
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = null;
        pageLoadVersion++;
        PageIndex = 0;
        Scene = null;
        PageImage = null;
        StatusText = $"共 {plan.Pages.Count} 页，正在等待预览。";
    }

    public async Task LoadPageAsync(CancellationToken cancellationToken = default)
    {
        PrintJobPlan? currentPlan = plan;
        if (currentPlan is null || currentPlan.Pages.Count == 0) return;
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token = pageCancellation.Token;
        int version = ++pageLoadVersion;
        int requestedIndex = PageIndex;
        StatusText = $"正在生成第 {requestedIndex + 1} 页预览…";
        try
        {
            RenderScene loadedScene = await previewService
                .GetPageAsync(currentPlan, requestedIndex, RenderTarget.Preview, token)
                .ConfigureAwait(true);
            byte[] png = await Task.Run(() => RenderPng(loadedScene, token), token).ConfigureAwait(true);
            if (version != pageLoadVersion || token.IsCancellationRequested) return;

            using MemoryStream stream = new(png, writable: false);
            BitmapImage source = new();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.StreamSource = stream;
            source.EndInit();
            source.Freeze();
            Scene = loadedScene;
            PageImage = source;
            StatusText = $"第 {requestedIndex + 1} / {currentPlan.Pages.Count} 页，{currentPlan.Pages[requestedIndex].Placements.Count} 个版位";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task PreviousPageAsync() { PageIndex--; await LoadPageAsync(); }
    private async Task NextPageAsync() { PageIndex++; await LoadPageAsync(); }

    private byte[] RenderPng(RenderScene loadedScene, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using SKBitmap bitmap = bitmapRenderer.Render(loadedScene.Pages[0], 96, assetProvider);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 90);
        cancellationToken.ThrowIfCancellationRequested();
        return data.ToArray();
    }

    public void Dispose()
    {
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = null;
    }
}
