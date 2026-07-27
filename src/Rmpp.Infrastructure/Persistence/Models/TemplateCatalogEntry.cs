namespace Rmpp.Infrastructure.Persistence.Models;

public enum TemplateCatalogStatus
{
    Available,
    Missing,
    DuplicateIdentity,
    Invalid,
}

/// <summary>表示可由 `.rmpp` 文件重新扫描生成的模板目录元数据。</summary>
public sealed record TemplateCatalogEntry
{
    /// <summary>数据库内部标识，新记录为0。</summary>
    public long Id { get; init; }
    /// <summary>模板正文中的稳定文档标识。</summary>
    public required Guid DocumentId { get; init; }
    /// <summary>规范化后的本地模板绝对路径。</summary>
    public required string Path { get; init; }
    /// <summary>模板标题。</summary>
    public required string Title { get; init; }
    /// <summary>模板说明。</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>模板文件最后修改时间。</summary>
    public DateTimeOffset FileModifiedAt { get; init; }
    /// <summary>模板正文最后修改时间。</summary>
    public DateTimeOffset DocumentModifiedAt { get; init; }
    /// <summary>目录最后扫描时间。</summary>
    public DateTimeOffset LastScannedAt { get; init; }
    /// <summary>文件可用、丢失、身份重复或无效状态。</summary>
    public TemplateCatalogStatus Status { get; init; }
    /// <summary>可选PNG缩略图缓存，不是模板正文。</summary>
    public byte[]? ThumbnailPng { get; init; }
    /// <summary>本地目录标签。</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
