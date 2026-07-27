using SkiaSharp;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Skia;

/// <summary>按 72 点/英寸导出具有精确物理页面尺寸的本地 PDF。</summary>
public sealed class SkiaPdfExporter(SkiaSceneRenderer? sceneRenderer = null)
{
    private const double PointsPerMillimetre = 72 / 25.4;
    private readonly SkiaSceneRenderer sceneRenderer = sceneRenderer ?? new SkiaSceneRenderer();

    public void Export(
        RenderScene scene,
        Stream destination,
        IRenderAssetProvider? assetProvider = null,
        SkiaRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(destination);
        using SKDocument document = SKDocument.CreatePdf(destination)
            ?? throw new InvalidOperationException("Unable to create PDF document.");
        foreach (RenderPage page in scene.Pages)
        {
            float widthPoints = checked((float)(page.Size.Width * PointsPerMillimetre));
            float heightPoints = checked((float)(page.Size.Height * PointsPerMillimetre));
            SKCanvas canvas = document.BeginPage(widthPoints, heightPoints);
            sceneRenderer.RenderPage(canvas, page, PointsPerMillimetre, assetProvider, options);
            document.EndPage();
        }

        document.Close();
    }
}
