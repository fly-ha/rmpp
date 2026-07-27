using Rmpp.Domain.Geometry;

namespace Rmpp.Rendering.Scene;

/// <summary>表示命令可选的矩形或路径裁剪区域。</summary>
public sealed record RenderClip
{
    public MmRect? Rectangle { get; init; }
    public RenderPath? Path { get; init; }

    public static RenderClip FromRectangle(MmRect rectangle) => new() { Rectangle = rectangle };
    public static RenderClip FromPath(RenderPath path) => new() { Path = path };
}
