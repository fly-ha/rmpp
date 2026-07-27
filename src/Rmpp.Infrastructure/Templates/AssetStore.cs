using System.Buffers.Binary;
using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>集中执行包内图片和 PDF 资源的类型、命名、内容头及尺寸限制。</summary>
public sealed class AssetStore
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly Dictionary<string, string[]> SupportedExtensions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["image/png"] = [".png"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["application/pdf"] = [".pdf"],
        };

    private readonly PackageLimits limits;

    public AssetStore(PackageLimits? limits = null)
    {
        this.limits = limits ?? new PackageLimits();
    }

    public AssetDescriptor Inspect(AssetReference reference, ReadOnlySpan<byte> content)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ValidateFileName(reference.FileName);
        string extension = ValidateMediaTypeAndExtension(reference.MediaType, reference.FileName);
        if (content.Length > limits.MaximumAssetBytes)
        {
            throw new RmppPackageException($"Asset exceeds size limit: {reference.FileName}");
        }

        ValidateContent(reference.MediaType, content, reference.FileName);
        string hash = AssetHashService.ComputeSha256(content);
        if (!string.Equals(hash, reference.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new RmppPackageException($"Asset hash does not match document metadata: {reference.FileName}");
        }

        string canonicalExtension = string.Equals(reference.MediaType, "image/jpeg", StringComparison.Ordinal)
            ? ".jpg"
            : extension;
        return new AssetDescriptor(
            reference.Id,
            reference.FileName,
            reference.MediaType,
            canonicalExtension,
            content.Length,
            hash);
    }

    public AssetDescriptor Validate(
        RmppManifestAsset manifestAsset,
        AssetReference reference,
        ReadOnlySpan<byte> content)
    {
        AssetDescriptor descriptor = Inspect(reference, content);
        if (manifestAsset.Id != descriptor.Id ||
            !string.Equals(manifestAsset.EntryPath, descriptor.EntryPath, StringComparison.Ordinal) ||
            !string.Equals(manifestAsset.FileName, descriptor.FileName, StringComparison.Ordinal) ||
            !string.Equals(manifestAsset.MediaType, descriptor.MediaType, StringComparison.Ordinal) ||
            manifestAsset.Length != descriptor.Length ||
            !string.Equals(manifestAsset.Sha256, descriptor.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new RmppPackageException($"Asset manifest metadata is inconsistent: {manifestAsset.EntryPath}");
        }

        return descriptor;
    }

    public void ValidatePreviewPng(ReadOnlySpan<byte> content) => ValidatePng(content, "preview.png");

    private static void ValidateFileName(string fileName)
    {
        if (fileName.Length > 255 ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            fileName.Any(char.IsControl))
        {
            throw new RmppPackageException("Asset file name is unsafe.");
        }
    }

    private static string ValidateMediaTypeAndExtension(string mediaType, string fileName)
    {
        if (!SupportedExtensions.TryGetValue(mediaType, out string[]? extensions))
        {
            throw new RmppPackageException($"Unsupported asset media type: {mediaType}");
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!extensions.Contains(extension, StringComparer.Ordinal))
        {
            throw new RmppPackageException($"Asset extension does not match media type: {fileName}");
        }

        return extension;
    }

    private void ValidateContent(string mediaType, ReadOnlySpan<byte> content, string fileName)
    {
        switch (mediaType)
        {
            case "image/png":
                ValidatePng(content, fileName);
                break;
            case "image/jpeg":
                ValidateJpeg(content, fileName);
                break;
            case "application/pdf":
                if (content.Length < 5 || !content[..5].SequenceEqual("%PDF-"u8))
                {
                    throw new RmppPackageException($"Asset content is not a valid PDF header: {fileName}");
                }

                break;
            default:
                throw new RmppPackageException($"Unsupported asset media type: {mediaType}");
        }
    }

    private void ValidatePng(ReadOnlySpan<byte> content, string fileName)
    {
        if (content.Length < 24 ||
            !content[..PngSignature.Length].SequenceEqual(PngSignature) ||
            !content.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new RmppPackageException($"Asset content is not a valid PNG header: {fileName}");
        }

        int width = BinaryPrimitives.ReadInt32BigEndian(content.Slice(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(content.Slice(20, 4));
        ValidateImageDimensions(width, height, fileName);
    }

    private void ValidateJpeg(ReadOnlySpan<byte> content, string fileName)
    {
        if (content.Length < 4 || content[0] != 0xff || content[1] != 0xd8)
        {
            throw new RmppPackageException($"Asset content is not a valid JPEG header: {fileName}");
        }

        int offset = 2;
        while (offset + 3 < content.Length)
        {
            while (offset < content.Length && content[offset] != 0xff)
            {
                offset++;
            }

            while (offset < content.Length && content[offset] == 0xff)
            {
                offset++;
            }

            if (offset >= content.Length)
            {
                break;
            }

            byte marker = content[offset++];
            if (marker is 0xd8 or 0xd9 || marker is >= 0xd0 and <= 0xd7)
            {
                continue;
            }

            if (offset + 2 > content.Length)
            {
                break;
            }

            int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > content.Length)
            {
                break;
            }

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 7)
                {
                    break;
                }

                int height = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 3, 2));
                int width = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 5, 2));
                ValidateImageDimensions(width, height, fileName);
                return;
            }

            offset += segmentLength;
        }

        throw new RmppPackageException($"JPEG dimensions cannot be read safely: {fileName}");
    }

    private void ValidateImageDimensions(int width, int height, string fileName)
    {
        if (width <= 0 || height <= 0 ||
            width > limits.MaximumImageWidthPixels ||
            height > limits.MaximumImageHeightPixels ||
            (long)width * height > limits.MaximumImagePixels)
        {
            throw new RmppPackageException($"Image dimensions exceed safe limits: {fileName}");
        }
    }

    private static bool IsStartOfFrame(byte marker) => marker is
        0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or
        0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;
}
