namespace Rmpp.Application.Data;

/// <summary>保存一条不可变导入记录；索引从零开始，值保持源文件显示文本。</summary>
public sealed record DataRowSnapshot
{
    public required int Index { get; init; }
    public required IReadOnlyDictionary<string, string?> Values { get; init; }

    public string? GetValue(string fieldName) =>
        Values.TryGetValue(fieldName, out string? value) ? value : null;

    /// <summary>诊断文本只包含索引和字段数，不包含任何客户数据值。</summary>
    public override string ToString() =>
        $"DataRowSnapshot {{ Index = {Index}, FieldCount = {Values.Count} }}";
}
