namespace Rmpp.Infrastructure.Templates;

/// <summary>描述模板包版本、文档身份及资源完整性信息。</summary>
public sealed record RmppManifest
{
    public string Format { get; init; } = TemplateFormatVersion.FormatIdentifier;
    public int SchemaVersion { get; init; } = TemplateFormatVersion.CurrentSchemaVersion;
    public string MinimumAppVersion { get; init; } = TemplateFormatVersion.CurrentApplicationVersion;
    public Guid DocumentId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public IReadOnlyList<RmppManifestAsset> Assets { get; init; } = Array.Empty<RmppManifestAsset>();
}

/// <summary>记录包内资源的路径、类型、长度和SHA-256摘要。</summary>
public sealed record RmppManifestAsset(
    Guid Id,
    string EntryPath,
    string FileName,
    string MediaType,
    long Length,
    string Sha256);
