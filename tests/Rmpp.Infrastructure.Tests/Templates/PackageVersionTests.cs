using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Templates;

public sealed class PackageVersionTests
{
    [Theory]
    [InlineData("manifest.json")]
    [InlineData("document.json")]
    public async Task NewerSchemaVersionIsRejected(string entryPath)
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        PackageArchiveTestHelper.ReplaceTextEntry(
            stream,
            entryPath,
            static json => json.Replace(
                "\"schemaVersion\": 1",
                "\"schemaVersion\": 2",
                StringComparison.Ordinal));

        stream.Position = 0;
        await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
    }

    [Fact]
    public async Task NewerDocumentSchemaIsRejectedBeforePolymorphicDeserialization()
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        PackageArchiveTestHelper.ReplaceTextEntry(
            stream,
            "document.json",
            static json => json
                .Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal)
                .Replace("\"$type\": \"polygon\"", "\"$type\": \"future-shape\"", StringComparison.Ordinal));

        stream.Position = 0;
        RmppPackageException exception = await Assert.ThrowsAsync<RmppPackageException>(
            () => new RmppPackageReader().ReadAsync(stream));

        Assert.Contains("requires a newer application", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("manifest.json")]
    [InlineData("document.json")]
    public async Task NewerMinimumApplicationVersionIsRejected(string entryPath)
    {
        await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
        PackageArchiveTestHelper.ReplaceTextEntry(
            stream,
            entryPath,
            static json => json.Replace(
                "\"minimumAppVersion\": \"1.0.0\"",
                "\"minimumAppVersion\": \"2.0.0\"",
                StringComparison.Ordinal));

        stream.Position = 0;
        await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
    }
}
