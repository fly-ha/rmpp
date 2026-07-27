namespace Rmpp.Infrastructure.Templates;

/// <summary>描述已经过类型、扩展名、长度、内容头和哈希校验的包内资源。</summary>
public sealed record AssetDescriptor(
    Guid Id,
    string FileName,
    string MediaType,
    string Extension,
    long Length,
    string Sha256)
{
    public string EntryPath => $"assets/{Id:N}{Extension}";
}
