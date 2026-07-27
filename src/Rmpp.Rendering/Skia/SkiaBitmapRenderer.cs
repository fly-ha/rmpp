using SkiaSharp;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Skia;

/// <summary>按显式 DPI 将毫米页面渲染为 BGRA 位图。</summary>
public sealed class SkiaBitmapRenderer(SkiaSceneRenderer? sceneRenderer = null)
{
    private readonly SkiaSceneRenderer sceneRenderer = sceneRenderer ?? new SkiaSceneRenderer();

    public SKBitmap Render(
        RenderPage page,
        double dpi,
        IRenderAssetProvider? assetProvider = null,
        SkiaRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!double.IsFinite(dpi) || dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi));
        }

        int width = checked((int)Math.Ceiling(page.Size.Width * dpi / 25.4));
        int height = checked((int)Math.Ceiling(page.Size.Height * dpi / 25.4));
        SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using SKCanvas canvas = new(bitmap);
        SkiaRenderOptions effectiveOptions = options ?? new SkiaRenderOptions();
        canvas.Clear(new SKColor(
            effectiveOptions.PageColor.Red,
            effectiveOptions.PageColor.Green,
            effectiveOptions.PageColor.Blue,
            effectiveOptions.PageColor.Alpha));
        sceneRenderer.RenderPage(canvas, page, dpi / 25.4, assetProvider, effectiveOptions);
        canvas.Flush();
        return bitmap;
    }
}
