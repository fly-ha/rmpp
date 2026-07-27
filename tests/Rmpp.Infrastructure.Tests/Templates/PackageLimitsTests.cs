using System.Buffers.Binary;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Templates;

public sealed class PackageLimitsTests
{
    [Fact]
    public async Task ExcessiveEntryCountIsRejected()
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        stream.Position = 0;

        RmppPackageReader reader = new(new PackageLimits { MaximumEntries = 3 });
        await Assert.ThrowsAsync<RmppPackageException>(() => reader.ReadAsync(stream));
    }

    [Fact]
    public async Task ExcessiveTotalUncompressedSizeIsRejected()
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        stream.Position = 0;

        RmppPackageReader reader = new(new PackageLimits { MaximumTotalUncompressedBytes = 64 });
        await Assert.ThrowsAsync<RmppPackageException>(() => reader.ReadAsync(stream));
    }

    [Fact]
    public void ExcessiveImageDimensionsAreRejected()
    {
        byte[] pngHeader = CreatePngHeader(width: 40_000, height: 1);
        AssetReference asset = new(
            Guid.NewGuid(),
            "wide.png",
            "image/png",
            AssetHashService.ComputeSha256(pngHeader));

        Assert.Throws<RmppPackageException>(() => new AssetStore().Inspect(asset, pngHeader));
    }

    [Fact]
    public void UnsupportedAssetMediaTypeIsRejected()
    {
        byte[] content = [1, 2, 3];
        AssetReference asset = new(
            Guid.NewGuid(),
            "payload.bin",
            "application/octet-stream",
            AssetHashService.ComputeSha256(content));

        Assert.Throws<RmppPackageException>(() => new AssetStore().Inspect(asset, content));
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        byte[] bytes = new byte[24];
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        signature.CopyTo(bytes, 0);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }
}
