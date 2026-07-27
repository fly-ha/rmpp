using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Snapping;

/// <summary>描述一个源锚点被调整到目标候选的结果。</summary>
public sealed record SnapMatch(
    double SourcePositionMm,
    double AdjustmentMm,
    SnapCandidate Candidate);

/// <summary>返回调整后的移动量及供设计器绘制的水平、垂直指示。</summary>
public sealed record SnapResult(
    MmPoint AdjustedDelta,
    SnapMatch? HorizontalMatch,
    SnapMatch? VerticalMatch)
{
    public bool Snapped => HorizontalMatch is not null || VerticalMatch is not null;
}
