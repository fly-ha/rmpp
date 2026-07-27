using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Printing;

/// <summary>保存一个真实输出页面及其有序标签放置，不保存渲染位图。</summary>
public sealed record PlannedPhysicalPage
{
    public required int PageNumber { get; init; }
    public required MmSize Size { get; init; }
    public IReadOnlyList<PlannedPlacement> Placements { get; init; } = Array.Empty<PlannedPlacement>();
}
