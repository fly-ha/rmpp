using Rmpp.Rendering.Images;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Skia;

/// <summary>把场景图片引用解析为已验证位图；实现可来自本地路径、模板包或 PDF 栅格结果。</summary>
public interface IRenderAssetProvider
{
    DecodedImage? Load(RenderImage image, double targetDpi);
}

public sealed class FileSystemRenderAssetProvider(ImageDecoder? decoder = null) : IRenderAssetProvider
{
    private readonly ImageDecoder decoder = decoder ?? new ImageDecoder();

    public DecodedImage? Load(RenderImage image, double targetDpi)
    {
        ArgumentNullException.ThrowIfNull(image);
        return string.IsNullOrWhiteSpace(image.LocalPath) || !File.Exists(image.LocalPath)
            ? null
            : decoder.DecodeFile(image.LocalPath);
    }
}
