using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;

namespace Rmpp.Rendering.Scene;

/// <summary>描述尚未由后端解码的稳定资源或本地变量图片引用。</summary>
public sealed record RenderImage
{
    public Guid? AssetId { get; init; }
    public string? LocalPath { get; init; }
    public ImageFitMode FitMode { get; init; }
    public MmRect? Crop { get; init; }
    public int? PdfPageNumber { get; init; }
}
