using Rmpp.Application.Validation;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Layout;
using Rmpp.Rendering.Layout;
using Rmpp.Rendering.Scene;

namespace Rmpp.Application.Printing;

/// <summary>表示一条记录副本在物理页或标签格中的确定位置和已解析元素值。</summary>
public sealed record PlannedPlacement
{
    public required int PlacementIndex { get; init; }
    public required int PageNumber { get; init; }
    public required int RecordIndex { get; init; }
    public required int RecordCopyIndex { get; init; }
    public required int JobCopyIndex { get; init; }
    public required LabelCell Cell { get; init; }
    public required MmSize ContentSize { get; init; }
    public required RenderTransform Transform { get; init; }
    public IReadOnlyList<ResolvedElement> ResolvedElements { get; init; } = Array.Empty<ResolvedElement>();
    public IReadOnlyList<ValidationIssue> Issues { get; init; } = Array.Empty<ValidationIssue>();
}
