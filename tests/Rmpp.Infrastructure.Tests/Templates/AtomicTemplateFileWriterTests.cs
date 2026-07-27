using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Templates;

public sealed class AtomicTemplateFileWriterTests
{
    [Fact]
    public async Task SuccessfulWriteCreatesReadablePackage()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "sample.rmpp");
        try
        {
            await new AtomicTemplateFileWriter().WriteAsync(path, TemplatePackageTestData.Create());
            await using FileStream stream = File.OpenRead(path);
            TemplatePackageContent loaded = await new RmppPackageReader().ReadAsync(stream);
            Assert.Equal("Package test", loaded.Document.Metadata.Title);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FailedWritePreservesExistingDestination()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "sample.rmpp");
        byte[] original = [9, 8, 7];
        await File.WriteAllBytesAsync(path, original);
        try
        {
            TemplatePackageContent valid = TemplatePackageTestData.Create();
            AssetReference asset = valid.Document.Assets[0];
            TemplatePackageContent invalid = valid with
            {
                Document = valid.Document with
                {
                    Assets =
                    [
                        new AssetReference(
                            asset.Id,
                            asset.FileName,
                            asset.MediaType,
                            new string('0', 64)),
                    ],
                },
            };

            await Assert.ThrowsAsync<RmppPackageException>(() => new AtomicTemplateFileWriter().WriteAsync(path, invalid));
            Assert.Equal(original, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "rmpp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
