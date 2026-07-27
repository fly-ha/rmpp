using SkiaSharp;
using Rmpp.Rendering.Images;
using Xunit;

namespace Rmpp.Rendering.Tests.Images;

public sealed class ImageDecoderTests
{
    [Fact]
    public void PngDecodesAfterBoundsValidation()
    {
        using MemoryStream stream = CreatePng(8, 6);
        using DecodedImage image = new ImageDecoder().Decode(stream);

        Assert.Equal(8, image.Width);
        Assert.Equal(6, image.Height);
    }

    [Fact]
    public void ExcessiveDimensionsAreRejectedBeforeFullDecode()
    {
        using MemoryStream stream = CreatePng(8, 6);
        ImageDecoder decoder = new(new ImageDecodeLimits
        {
            MaximumWidthPixels = 7,
            MaximumHeightPixels = 10,
            MaximumPixels = 100,
        });

        Assert.Throws<InvalidDataException>(() => decoder.Decode(stream));
    }

    private static MemoryStream CreatePng(int width, int height)
    {
        using SKBitmap bitmap = new(new SKImageInfo(width, height));
        bitmap.Erase(SKColors.CornflowerBlue);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(data.ToArray());
    }
}
