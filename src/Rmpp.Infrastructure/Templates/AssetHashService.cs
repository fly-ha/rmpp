using System.Security.Cryptography;

namespace Rmpp.Infrastructure.Templates;

/// <summary>以小写十六进制SHA-256校验模板资源完整性。</summary>
public static class AssetHashService
{
    public static string ComputeSha256(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
