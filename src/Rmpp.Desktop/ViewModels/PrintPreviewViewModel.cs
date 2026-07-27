using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Rmpp.Application.Printing;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using SkiaSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rmpp.Desktop.ViewModels;

/// <summary>按需渲染 immutable 打印计划中的当前物理页，并保持有界缓存。</summary>
public sealed partial class PrintPreviewViewModel : ObservableObject
{
    private readonly PrintPreviewService previewService;
    private readonly SkiaBitmapRenderer bitmapRenderer;
    private PrintJobPlan? plan;

    public PrintPreviewViewModel(PrintPreviewService? previewService = null, SkiaBitmapRenderer? bitmapRenderer = null)
    {
        this.previewService = previewService ?? new PrintPreviewService();
        this.bitmapRenderer = bitmapRenderer ?? new SkiaBitmapRenderer();
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
        PageIndex = 0;
        _ = LoadPageAsync();
    }

    public async Task LoadPageAsync(CancellationToken cancellationToken = default)
    {
        if (plan is null || plan.Pages.Count == 0) return;
        Scene = await previewService.GetPageAsync(plan, PageIndex, RenderTarget.Preview, cancellationToken).ConfigureAwait(true);
        using SKBitmap bitmap = bitmapRenderer.Render(Scene.Pages[0], 96);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 90);
        using MemoryStream stream = new(data.ToArray());
        BitmapImage source = new();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.StreamSource = stream;
        source.EndInit();
        source.Freeze();
        PageImage = source;
        StatusText = $"第 {PageIndex + 1} / {plan.Pages.Count} 页，{plan.Pages[PageIndex].Placements.Count} 个版位";
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private async Task PreviousPageAsync() { PageIndex--; await LoadPageAsync(); }
    private async Task NextPageAsync() { PageIndex++; await LoadPageAsync(); }
}
