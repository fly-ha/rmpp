using System.IO.Compression;
using System.Text;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Templates;

public sealed class PackageSecurityTests
{
    [Fact]
    public async Task TraversalEntryIsRejected()
    {
        await using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "../escape.txt", "unsafe");
            WriteEntry(archive, "manifest.json", "{}");
            WriteEntry(archive, "document.json", "{}");
        }

        stream.Position = 0;
        await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
    }

    [Fact]
    public async Task ModifiedAssetIsRejected()
    {
        TemplatePackageContent source = TemplatePackageTestData.Create();
        await using MemoryStream stream = new();
        await new RmppPackageWriter().WriteAsync(stream, source);
        stream.Position = 0;
        using (ZipArchive archive = new(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            ZipArchiveEntry asset = Assert.Single(
                archive.Entries,
                static entry => entry.FullName.StartsWith("assets/", StringComparison.Ordinal));
            string path = asset.FullName;
            asset.Delete();
            WriteEntry(archive, path, "tampered");
        }

        stream.Position = 0;
        await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
    }

    [Fact]
    public async Task DuplicateCriticalEntryIsRejected()
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        stream.Position = 0;
        using (ZipArchive archive = new(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            PackageArchiveTestHelper.WriteTextEntry(archive, "MANIFEST.JSON", "{}");
        }

        stream.Position = 0;
        await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
    }

    [Fact]
    public async Task InvalidElementDiscriminatorIsRejected()
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        PackageArchiveTestHelper.ReplaceTextEntry(
            stream,
            "document.json",
            static json => json.Replace(
                "\"$type\": \"polygon\"",
                "\"$type\": \"executable\"",
                StringComparison.Ordinal));

        stream.Position = 0;
        await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using Stream output = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        output.Write(bytes);
    }
}
