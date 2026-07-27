using System.Runtime.CompilerServices;

namespace Rmpp.Application.Data;

/// <summary>保存一次导入的会话内不可变字段和记录，不进入模板、SQLite、日志或恢复文件。</summary>
public sealed record DataSetSnapshot : IDataPreviewSource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SourceDisplayName { get; init; }
    public required DataSchema Schema { get; init; }
    public required IReadOnlyList<DataRowSnapshot> Rows { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool IsTruncated { get; init; }

    public int Count => Rows.Count;

    /// <summary>只输出来源和规模，防止结构化日志通过 record 默认文本泄露导入行。</summary>
    public override string ToString() =>
        $"DataSetSnapshot {{ Id = {Id}, Source = {SourceDisplayName}, Columns = {Schema.Columns.Count}, Rows = {Rows.Count}, Truncated = {IsTruncated} }}";

    public async IAsyncEnumerable<DataRowSnapshot> ReadRangeAsync(
        int offset,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        int end = Math.Min(Rows.Count, checked(offset + count));
        for (int index = offset; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Rows[index];
            await Task.Yield();
        }
    }
}
