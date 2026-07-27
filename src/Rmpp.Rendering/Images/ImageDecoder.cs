using SkiaSharp;

namespace Rmpp.Rendering.Images;

/// <summary>在完整解码前读取图片边界并执行像素限制。</summary>
public sealed class ImageDecoder(ImageDecodeLimits? limits = null)
{
    private readonly ImageDecodeLimits limits = limits ?? new ImageDecodeLimits();

    public DecodedImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        byte[] bytes = copy.ToArray();
        SKImageInfo bounds = SKBitmap.DecodeBounds(bytes);
        Validate(bounds.Width, bounds.Height);
        SKBitmap bitmap = SKBitmap.Decode(bytes)
            ?? throw new InvalidDataException("Image data is not a supported PNG or JPEG image.");
        if (bitmap.Width != bounds.Width || bitmap.Height != bounds.Height)
        {
            bitmap.Dispose();
            throw new InvalidDataException("Decoded image dimensions changed unexpectedly.");
        }

        return new DecodedImage(bitmap);
    }

    public DecodedImage DecodeFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Decode(stream);
    }

    private void Validate(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Image dimensions are invalid.");
        }

        long pixels = checked((long)width * height);
        if (width > limits.MaximumWidthPixels || height > limits.MaximumHeightPixels || pixels > limits.MaximumPixels)
        {
            throw new InvalidDataException("Image dimensions exceed configured safety limits.");
        }
    }
}
