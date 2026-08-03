using Rmpp.Application.Abstractions;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;

namespace Rmpp.Infrastructure.Pdf;

/// <summary>使用本地 Skia 后端实现 PDF 端口，不依赖虚拟打印机或网络服务。</summary>
public sealed class LocalRenderExporter(SkiaPdfExporter? exporter = null) : IRenderExporter
{
    private readonly SkiaPdfExporter exporter = exporter ?? new SkiaPdfExporter();

    public async Task ExportAsync(
        RenderScene scene,
        Stream destination,
        RenderExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(options.Format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"不支持的本地导出格式：{options.Format}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        int first = options.FirstPage ?? 1;
        int last = options.LastPage ?? scene.Pages.Count;
        if (first <= 0 || last < first || last > scene.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "PDF 页码范围无效。");
        }

        if (!double.IsFinite(options.ImageSourceDpi) || options.ImageSourceDpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Image source DPI must be positive and finite.");
        }

        RenderPage[] pages = scene.Pages.Skip(first - 1).Take(last - first + 1).ToArray();
        RenderScene selected = scene with
        {
            Pages = pages,
            Issues = pages.SelectMany(static page => page.Issues).ToArray(),
        };
        exporter.Export(
            selected,
            destination,
            options.AssetProvider,
            new SkiaRenderOptions { ImageSourceDpi = options.ImageSourceDpi });
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
