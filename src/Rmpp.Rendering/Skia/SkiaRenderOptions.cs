using Rmpp.Domain.Styles;

namespace Rmpp.Rendering.Skia;

/// <summary>定义 Skia 输出的页面底色、图片 DPI 和缺失资源策略。</summary>
public sealed record SkiaRenderOptions
{
    public RgbaColor PageColor { get; init; } = RgbaColor.White;
    public double ImageSourceDpi { get; init; } = 96;
    public bool IgnoreMissingAssets { get; init; }
}
