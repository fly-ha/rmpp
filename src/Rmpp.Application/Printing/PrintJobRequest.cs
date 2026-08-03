using Rmpp.Application.Data;
using Rmpp.Domain.Documents;

namespace Rmpp.Application.Printing;

/// <summary>定义生成不可变任务计划所需的文档、会话数据、范围和副本策略。</summary>
public sealed record PrintJobRequest
{
    public required TemplateDocument Document { get; init; }
    public DataSetSnapshot? DataSet { get; init; }
    public PrintRecordSelection RecordSelection { get; init; } = PrintRecordSelection.All;
    public PrintCopyPolicy CopyPolicy { get; init; } = new();
    /// <summary>没有外部数据源时生成的逻辑输出数量；每个位置可获得独立流水号。</summary>
    public int OutputCount { get; init; } = 1;
    public DateTimeOffset? ReferenceTime { get; init; }
    public string CultureName { get; init; } = string.Empty;
    public int? StartingCellOverride { get; init; }
}
