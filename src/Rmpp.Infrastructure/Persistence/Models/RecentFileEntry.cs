namespace Rmpp.Infrastructure.Persistence.Models;

public enum RecentFileKind
{
    Template,
    DataSource,
}

/// <summary>只记录本地路径和显示名称的最近文件项，不包含导入数据行。</summary>
public sealed record RecentFileEntry
{
    /// <summary>模板或数据源类别。</summary>
    public required RecentFileKind Kind { get; init; }
    /// <summary>规范化后的本地绝对路径。</summary>
    public required string Path { get; init; }
    /// <summary>用户界面显示名称。</summary>
    public required string DisplayName { get; init; }
    /// <summary>最后打开时间。</summary>
    public DateTimeOffset LastOpenedAt { get; init; }
}
