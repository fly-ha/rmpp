using Rmpp.Domain.Elements;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Templates;

public sealed class PackageRoundTripTests
{
    [Fact]
    public async Task WriterAndReaderPreserveDocumentTypesAndAssets()
    {
        TemplatePackageContent source = TemplatePackageTestData.Create();
        await using MemoryStream stream = new();
        await new RmppPackageWriter().WriteAsync(stream, source);
        string documentJson = PackageArchiveTestHelper.ReadTextEntry(stream, "document.json");
        Assert.Contains("\"$type\": \"polygon\"", documentJson, StringComparison.Ordinal);
        Assert.Contains("Hatched polygon", documentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"isEmpty\"", documentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"location\"", documentJson, StringComparison.Ordinal);
        stream.Position = 0;

        TemplatePackageContent result = await new RmppPackageReader().ReadAsync(stream);

        Assert.Equal(source.Document.Id, result.Document.Id);
        Assert.Equal(3, result.Document.Elements.Count);
        PolygonElement polygon = Assert.IsType<PolygonElement>(result.Document.Elements[1]);
        Assert.IsType<HatchFill>(polygon.Fill);
        Assert.Equal(source.Assets.Single().Value, result.Assets.Single().Value);
        Assert.Equal(source.PreviewPng, result.PreviewPng);
    }
}
