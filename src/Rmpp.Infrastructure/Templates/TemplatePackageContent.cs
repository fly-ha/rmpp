using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>表示已加载或待保存的模板文档、二进制资源和可选缩略图。</summary>
public sealed record TemplatePackageContent
{
    public required TemplateDocument Document { get; init; }
    public IReadOnlyDictionary<Guid, byte[]> Assets { get; init; } = new Dictionary<Guid, byte[]>();
    public byte[]? PreviewPng { get; init; }
}
