using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Snapping;

public enum SnapAxis
{
    Horizontal,
    Vertical,
}

public enum SnapCandidateKind
{
    Grid,
    Guide,
    PageEdge,
    PageCenter,
    ElementEdge,
    ElementCenter,
}

/// <summary>表示一条可吸附的水平或垂直毫米坐标。</summary>
public sealed record SnapCandidate(
    SnapAxis Axis,
    double PositionMm,
    SnapCandidateKind Kind,
    Guid? SourceId = null,
    string? Label = null);

/// <summary>定义吸附来源、屏幕容差和当前缩放换算。</summary>
public sealed record SnapOptions
{
    public bool IsEnabled { get; init; } = true;
    public bool IsTemporarilyDisabled { get; init; }
    public bool SnapToGrid { get; init; } = true;
    public bool SnapToGuides { get; init; } = true;
    public bool SnapToPage { get; init; } = true;
    public bool SnapToElements { get; init; } = true;
    public double GridSpacingMm { get; init; } = 5;
    public double TolerancePixels { get; init; } = 6;
    public double PixelsPerMillimetre { get; init; } = 96d / 25.4;
}

/// <summary>保存生成吸附候选所需的页面、参考线、元素和移动选择。</summary>
public sealed record SnapContext(
    MmSize PageSize,
    IReadOnlyList<GuideDefinition> Guides,
    IReadOnlyList<Rmpp.Domain.Elements.TemplateElement> Elements,
    IReadOnlySet<Guid> MovingElementIds);
