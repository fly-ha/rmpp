using Rmpp.Domain.Elements;
using Rmpp.Domain.Styles;
using Rmpp.Domain.Printing;
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
        Assert.Equal("printer-stable-id", result.Document.PrintSettings.PreferredPrinterId);
        Assert.Equal("办公室打印机", result.Document.PrintSettings.PreferredPrinterDisplayName);
        Assert.Equal(100, result.Document.PrintSettings.OutputCount);
        Assert.Equal(2, result.Document.PrintSettings.Copies);
        Assert.Equal(PrintCopyOrder.Collated, result.Document.PrintSettings.CopyOrder);
        Assert.Equal(3, result.Document.PrintSettings.StartingCell);
        Assert.False(result.Document.PrintSettings.IncludePrintableBackgrounds);
        Assert.Equal(4, result.Document.Elements.Count);
        PolygonElement polygon = Assert.IsType<PolygonElement>(result.Document.Elements[1]);
        Assert.IsType<HatchFill>(polygon.Fill);
        BarcodeElement barcode = Assert.IsType<BarcodeElement>(result.Document.Elements[3]);
        Assert.Equal(source.Document.Assets[0].Id, barcode.CenterIconAssetId);
        Assert.Equal(0.18, barcode.CenterIconScale);
        Assert.Equal(source.Assets.Single().Value, result.Assets.Single().Value);
        Assert.Equal(source.PreviewPng, result.PreviewPng);
    }
}
