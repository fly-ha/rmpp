using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Rendering.Scene;

/// <summary>所有后端共享的有序毫米制渲染命令基类。</summary>
public abstract record RenderCommand
{
    public required Guid SourceId { get; init; }
    public int ZIndex { get; init; }
    public double Opacity { get; init; } = 1;
    public RenderTransform Transform { get; init; } = RenderTransform.Identity;
    public RenderClip? Clip { get; init; }
}

public sealed record RenderPathCommand : RenderCommand
{
    public required RenderPath Path { get; init; }
    public required RenderStroke Stroke { get; init; }
    public required RenderFill Fill { get; init; }
}

public sealed record RenderTextCommand : RenderCommand
{
    public required string Text { get; init; }
    public required MmRect LocalBounds { get; init; }
    public required RenderTextStyle Style { get; init; }
    public IReadOnlyList<RenderGlyphRun> GlyphRuns { get; init; } = Array.Empty<RenderGlyphRun>();
}

public sealed record RenderImageCommand : RenderCommand
{
    public required RenderImage Image { get; init; }
    public required MmRect LocalBounds { get; init; }
    public required RenderStroke Border { get; init; }
    public required RenderFill Fill { get; init; }
}

public sealed record RenderBarcodeCommand : RenderCommand
{
    public required string Content { get; init; }
    public required BarcodeSymbology Symbology { get; init; }
    public required MmRect LocalBounds { get; init; }
    public double QuietZoneMm { get; init; }
    public int ErrorCorrectionLevel { get; init; }
    public bool ShowHumanReadableText { get; init; }
    public required RenderTextStyle HumanReadableTextStyle { get; init; }
    /// <summary>二维码中心的可选包内图片引用；后端必须在模块上方绘制同一白色保护区和图标。</summary>
    public RenderImage? CenterIcon { get; init; }
    public double CenterIconScale { get; init; } = BarcodeElement.DefaultCenterIconScale;
}
