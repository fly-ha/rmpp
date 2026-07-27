namespace Rmpp.Rendering.Images;

/// <summary>限制单张解码图片的边长和总像素，避免离线文件耗尽内存。</summary>
public sealed record ImageDecodeLimits
{
    public int MaximumWidthPixels { get; init; } = 32_768;
    public int MaximumHeightPixels { get; init; } = 32_768;
    public long MaximumPixels { get; init; } = 268_435_456;
}
