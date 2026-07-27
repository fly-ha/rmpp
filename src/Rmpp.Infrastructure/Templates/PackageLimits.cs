namespace Rmpp.Infrastructure.Templates;

/// <summary>限制不可信ZIP、JSON和资源的规模，避免解压炸弹和内存耗尽。</summary>
public sealed record PackageLimits
{
    public int MaximumEntries { get; init; } = 1024;
    public long MaximumTotalUncompressedBytes { get; init; } = 512L * 1024 * 1024;
    public long MaximumJsonBytes { get; init; } = 16L * 1024 * 1024;
    public long MaximumAssetBytes { get; init; } = 256L * 1024 * 1024;
    public int MaximumImageWidthPixels { get; init; } = 32_768;
    public int MaximumImageHeightPixels { get; init; } = 32_768;
    public long MaximumImagePixels { get; init; } = 268_435_456;
}
