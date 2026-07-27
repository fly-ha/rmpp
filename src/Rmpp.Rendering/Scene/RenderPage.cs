using Rmpp.Domain.Geometry;

namespace Rmpp.Rendering.Scene;

/// <summary>表示一张具有固定物理尺寸和稳定命令顺序的渲染页面。</summary>
public sealed record RenderPage
{
    public int PageNumber { get; init; }
    public required MmSize Size { get; init; }
    public required RenderClip Clip { get; init; }
    public required IReadOnlyList<RenderCommand> Commands { get; init; }
    public IReadOnlyList<RenderIssue> Issues { get; init; } = Array.Empty<RenderIssue>();
}
