using Rmpp.Domain.Data;

namespace Rmpp.Application.Data;

/// <summary>描述导入数据中的有序字段及其安全推断类型。</summary>
public sealed record DataColumnDefinition(
    string Name,
    int Ordinal,
    FieldDataType DataType = FieldDataType.Text,
    string? DisplayName = null);

/// <summary>保存大小写不敏感、名称唯一的数据字段集合。</summary>
public sealed record DataSchema
{
    public required IReadOnlyList<DataColumnDefinition> Columns { get; init; }

    public DataColumnDefinition? Find(string name) =>
        Columns.FirstOrDefault(column => StringComparer.OrdinalIgnoreCase.Equals(column.Name, name));
}
