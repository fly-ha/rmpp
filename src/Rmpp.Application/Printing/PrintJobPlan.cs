using Rmpp.Domain.Documents;

namespace Rmpp.Application.Printing;

/// <summary>预览、PDF和打印共同消费的不可变文档快照与物理页面计划。</summary>
public sealed record PrintJobPlan
{
    public required TemplateDocument DocumentSnapshot { get; init; }
    public required PrintJobContext Context { get; init; }
    public required IReadOnlyList<int> SelectedRecordIndices { get; init; }
    public required IReadOnlyList<PlannedPhysicalPage> Pages { get; init; }

    public int TotalPlacements => Pages.Sum(static page => page.Placements.Count);
}
