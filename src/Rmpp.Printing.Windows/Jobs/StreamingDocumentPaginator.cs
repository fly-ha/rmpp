using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Rmpp.Application.Abstractions;
using Rmpp.Domain.Printing;
using Rmpp.Printing.Windows.Calibration;
using Rmpp.Printing.Windows.Rendering;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;

namespace Rmpp.Printing.Windows.Jobs;

/// <summary>仅保留当前场景页的同步 paginator；XPS writer 拉取下一页时才渲染。</summary>
public sealed class StreamingDocumentPaginator : DocumentPaginator, IDisposable
{
    private readonly IAsyncEnumerator<RenderScene> scenes;
    private readonly Queue<RenderPage> pendingPages = new();
    private readonly WpfPrintSceneRenderer renderer;
    private readonly CalibrationProfile? calibration;
    private readonly IRenderAssetProvider? assetProvider;
    private readonly double imageSourceDpi;
    private readonly IProgress<PrintSubmissionProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private int renderedPages;
    private int nextPageNumber;
    private int? finalPageCount;
    private DocumentPage? lastPage;
    private int lastPageNumber = -1;
    private bool disposed;

    public StreamingDocumentPaginator(
        IAsyncEnumerable<RenderScene> scenes,
        WpfPrintSceneRenderer renderer,
        CalibrationProfile? calibration,
        Size pageSize,
        IProgress<PrintSubmissionProgress>? progress,
        CancellationToken cancellationToken,
        IRenderAssetProvider? assetProvider = null,
        double imageSourceDpi = 300)
    {
        this.scenes = scenes.GetAsyncEnumerator(cancellationToken);
        this.renderer = renderer;
        this.calibration = calibration;
        this.assetProvider = assetProvider;
        this.imageSourceDpi = imageSourceDpi;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
        PageSize = pageSize;
    }

    public override bool IsPageCountValid => finalPageCount.HasValue;
    public override int PageCount => finalPageCount ?? int.MaxValue;
    public override Size PageSize { get; set; }
    public override IDocumentPaginatorSource? Source => null;

    public override DocumentPage GetPage(int pageNumber)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (pageNumber == lastPageNumber && lastPage is not null)
        {
            return lastPage;
        }

        if (pageNumber != nextPageNumber)
        {
            throw new InvalidOperationException("Windows XPS writer 请求了非顺序页面，流式打印无法安全继续。");
        }

        RenderPage? page = TakeNextPage();
        if (page is null)
        {
            finalPageCount = nextPageNumber;
            return DocumentPage.Missing;
        }

        WpfRenderedPage rendered = renderer.Render(
            page,
            CalibrationTransform.Create(calibration, page.Size),
            assetProvider,
            imageSourceDpi);
        DocumentPage documentPage = new(
            rendered.Visual,
            rendered.Size,
            new Rect(new Point(0, 0), rendered.Size),
            new Rect(new Point(0, 0), rendered.Size));
        lastPage = documentPage;
        lastPageNumber = pageNumber;
        nextPageNumber++;
        renderedPages++;
        progress?.Report(new PrintSubmissionProgress(renderedPages, null, "页面已渲染并交给 Windows spooler。"));
        return documentPage;
    }

    public override void ComputePageCount()
    {
        // 有意不提前枚举；完整计数会破坏按页流式和取消语义。
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        scenes.DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private RenderPage? TakeNextPage()
    {
        while (pendingPages.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!scenes.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                return null;
            }

            foreach (RenderPage page in scenes.Current.Pages)
            {
                pendingPages.Enqueue(page);
            }
        }

        return pendingPages.Dequeue();
    }
}
