using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;

namespace Rmpp.Application.Abstractions;

/// <summary>定义本地导出的格式、页面范围和覆盖策略。</summary>
public sealed record RenderExportOptions
{
    public string Format { get; init; } = "pdf";
    public int? FirstPage { get; init; }
    public int? LastPage { get; init; }
    public IRenderAssetProvider? AssetProvider { get; init; }
    public double ImageSourceDpi { get; init; } = 300;
}

/// <summary>隔离 PDF 或其他本地文件导出后端。</summary>
public interface IRenderExporter
{
    Task ExportAsync(
        RenderScene scene,
        Stream destination,
        RenderExportOptions options,
        CancellationToken cancellationToken = default);
}
