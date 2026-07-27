using SkiaSharp;

namespace Rmpp.Rendering.Images;

/// <summary>包装已通过限制检查的 Skia 位图，并明确其释放责任。</summary>
public sealed class DecodedImage(SKBitmap bitmap) : IDisposable
{
    public SKBitmap Bitmap { get; } = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public void Dispose() => Bitmap.Dispose();
}
