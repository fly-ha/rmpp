using Rmpp.Domain.Documents;

namespace Rmpp.Application.Abstractions;

/// <summary>表示模板文档及其自包含资源内容，不包含任何会话导入数据。</summary>
public sealed record TemplatePackageContent
{
    public required TemplateDocument Document { get; init; }
    public IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> Assets { get; init; } =
        new Dictionary<Guid, ReadOnlyMemory<byte>>();
    public ReadOnlyMemory<byte>? PreviewPng { get; init; }
}

/// <summary>隔离开放、保存和验证 .rmpp 包的基础设施实现。</summary>
public interface ITemplatePackageService
{
    Task<TemplatePackageContent> OpenAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(
        string path,
        TemplatePackageContent package,
        CancellationToken cancellationToken = default);
}
