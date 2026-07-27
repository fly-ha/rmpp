namespace Rmpp.Infrastructure.Persistence.Models;

public enum RecoverySessionState
{
    Available,
    Restored,
    Discarded,
}

/// <summary>记录恢复包的位置与模板身份，不在SQLite中保存模板正文。</summary>
public sealed record RecoverySession
{
    /// <summary>恢复会话标识。</summary>
    public required Guid SessionId { get; init; }
    /// <summary>待恢复文档标识。</summary>
    public required Guid DocumentId { get; init; }
    /// <summary>原模板路径，可为空。</summary>
    public string? OriginalTemplatePath { get; init; }
    /// <summary>独立 `.rmpp` 恢复包路径。</summary>
    public required string RecoveryPackagePath { get; init; }
    /// <summary>恢复记录创建时间。</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>恢复记录最后更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; init; }
    /// <summary>可恢复、已恢复或已丢弃状态。</summary>
    public RecoverySessionState State { get; init; }
}
