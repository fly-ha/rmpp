using System.Runtime.InteropServices;
using Rmpp.Application.Abstractions;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Images;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using SkiaSharp;

namespace Rmpp.Infrastructure.Pdf;

/// <summary>把当前模板会话中的包内图片或 PDF 页解析为 Skia 可消费的像素，供预览、PDF 与打印共享。</summary>
public sealed class PackageRenderAssetProvider : IRenderAssetProvider
{
    private readonly TemplateDocument document;
    private readonly IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> contents;
    private readonly ImageDecoder imageDecoder;
    private readonly PdfBackgroundRasterizer pdfRasterizer;
    private readonly FileSystemRenderAssetProvider fileSystemProvider;

    public PackageRenderAssetProvider(
        TemplateDocument document,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> contents,
        ImageDecoder? imageDecoder = null,
        PdfBackgroundRasterizer? pdfRasterizer = null)
    {
        this.document = document ?? throw new ArgumentNullException(nameof(document));
        this.contents = contents ?? throw new ArgumentNullException(nameof(contents));
        this.imageDecoder = imageDecoder ?? new ImageDecoder();
        this.pdfRasterizer = pdfRasterizer ?? new PdfBackgroundRasterizer();
        fileSystemProvider = new FileSystemRenderAssetProvider(this.imageDecoder);
    }

    public DecodedImage? Load(RenderImage image, double targetDpi)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.AssetId is not { } assetId)
        {
            return fileSystemProvider.Load(image, targetDpi);
        }

        AssetReference? reference = document.Assets.FirstOrDefault(asset => asset.Id == assetId);
        if (reference is null || !contents.TryGetValue(assetId, out ReadOnlyMemory<byte> bytes) || bytes.IsEmpty)
        {
            return null;
        }

        if (!string.Equals(reference.MediaType, "application/pdf", StringComparison.Ordinal))
        {
            using MemoryStream stream = new(bytes.ToArray(), writable: false);
            return imageDecoder.Decode(stream);
        }

        BackgroundDefinition? background = document.Backgrounds
            .Where(item => item.AssetId == assetId && item.PdfPageNumber == (image.PdfPageNumber ?? 1))
            .OrderByDescending(static item => item.Bounds.Width * item.Bounds.Height)
            .FirstOrDefault();
        MmRect bounds = background?.Bounds ?? new MmRect(0, 0, 210, 297);
        double dpi = Math.Clamp(targetDpi, 72, 600);
        int width = Math.Clamp((int)Math.Ceiling(bounds.Width / 25.4 * dpi), 1, 8192);
        int height = Math.Clamp((int)Math.Ceiling(bounds.Height / 25.4 * dpi), 1, 8192);
        PdfRasterizedPage page = pdfRasterizer.RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = bytes,
            PageNumber = image.PdfPageNumber ?? 1,
            TargetWidthPixels = width,
            TargetHeightPixels = height,
        }).GetAwaiter().GetResult();
        return CreateDecodedImage(page);
    }

    private static DecodedImage CreateDecodedImage(PdfRasterizedPage page)
    {
        if (page.PixelFormat != PdfPixelFormat.Bgra32)
        {
            throw new InvalidDataException("Unsupported PDF raster pixel format.");
        }

        SKBitmap bitmap = new(new SKImageInfo(page.Width, page.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            byte[] pixels = page.Pixels.ToArray();
            if (pixels.Length != checked(page.Stride * page.Height))
            {
                throw new InvalidDataException("PDF raster pixel buffer has an invalid length.");
            }

            Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            return new DecodedImage(bitmap);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
