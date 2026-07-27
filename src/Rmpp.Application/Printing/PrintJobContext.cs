using Rmpp.Application.Data;

namespace Rmpp.Application.Printing;

/// <summary>冻结任务身份、创建时间、文化、任务时间和流水号计划。</summary>
public sealed record PrintJobContext
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public required DateTimeOffset CreatedAt { get; init; }
    public required JobTimeContext JobTime { get; init; }
    public required SerialPlan SerialPlan { get; init; }
}
