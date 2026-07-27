using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Text;

/// <summary>定义一次离线文本排版所需的文本、物理边界和样式。</summary>
public sealed record TextLayoutRequest
{
    public required string Text { get; init; }
    public required MmRect Bounds { get; init; }
    public required RenderTextStyle Style { get; init; }
    public double MinimumAutoFitPoints { get; init; } = 4;
}
