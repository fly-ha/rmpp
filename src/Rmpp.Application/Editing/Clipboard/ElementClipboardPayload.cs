using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Clipboard;

/// <summary>保存一个可跨文档粘贴的资源元数据和原始字节。</summary>
public sealed record ClipboardAsset(
    AssetReference Reference,
    ReadOnlyMemory<byte> Content);

/// <summary>保存剪贴板中的元素、资源和源文档身份，不携带会话数据行。</summary>
public sealed record ElementClipboardPayload
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public required Guid SourceDocumentId { get; init; }
    public IReadOnlyList<TemplateElement> Elements { get; init; } = Array.Empty<TemplateElement>();
    public IReadOnlyList<ClipboardAsset> Assets { get; init; } = Array.Empty<ClipboardAsset>();
}
