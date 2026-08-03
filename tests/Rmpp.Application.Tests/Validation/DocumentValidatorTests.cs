using Rmpp.Application.Documents;
using Rmpp.Application.Validation;
using Rmpp.Domain.Data;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Xunit;

namespace Rmpp.Application.Tests.Validation;

public sealed class DocumentValidatorTests
{
    [Fact]
    public void ReportsGeometryLayerAssetAndPrintableLocations()
    {
        Guid missingLayerId = Guid.NewGuid();
        Guid missingAssetId = Guid.NewGuid();
        ImageElement image = new()
        {
            LayerId = missingLayerId,
            AssetId = missingAssetId,
            Bounds = new MmRect(205, 290, 20, 20),
        };
        (TemplateDocument source, LayerDefinition layer) = TestDocumentFactory.Create();
        RectangleElement outside = TestDocumentFactory.Rectangle(x: 205, y: 290, width: 20, height: 20) with
        {
            LayerId = layer.Id,
        };
        TemplateDocument document = source with { Layers = [layer], Elements = [image, outside] };

        IReadOnlyList<ValidationIssue> issues = new DocumentValidator().Validate(document);

        ValidationIssue missingLayer = Assert.Single(issues, issue => issue.Code == "missing-layer");
        ValidationIssue missingAsset = Assert.Single(issues, issue => issue.Code == "missing-asset-reference");
        Assert.Equal(image.Id, missingLayer.Location.ElementId);
        Assert.Equal(missingAssetId, missingAsset.Location.AssetId);
        Assert.Contains(issues, issue => issue.Code == "outside-printable-page" && issue.Location.ElementId == outside.Id);
    }

    [Fact]
    public void ReusesRenderingDiagnosticsForFontOverflowAndBarcodeErrors()
    {
        TextElement text = new()
        {
            Bounds = new MmRect(0, 0, 3, 2),
            Content = ElementExpression.Literal("This text cannot fit"),
            TextStyle = new TextStyle
            {
                FontFamily = "RMPP Definitely Missing Font 92741",
                FontSizePoints = 24,
                Wrap = false,
            },
        };
        BarcodeElement barcode = new()
        {
            Bounds = new MmRect(10, 10, 30, 10),
            Symbology = BarcodeSymbology.Ean13,
            Content = ElementExpression.Literal("ABC"),
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(text, barcode);

        IReadOnlyList<ValidationIssue> issues = new DocumentValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == "missing-font" && issue.Location.ElementId == text.Id);
        Assert.Contains(issues, issue => issue.Code == "text-overflow" && issue.Location.ElementId == text.Id);
        Assert.Contains(issues, issue => issue.Code == "invalid-retail-barcode" && issue.Location.ElementId == barcode.Id);
    }

    [Fact]
    public void AssetContentIssuePointsToAsset()
    {
        AssetReference asset = new(Guid.NewGuid(), "missing.png", "image/png", new string('d', 64));
        (TemplateDocument source, _) = TestDocumentFactory.Create();
        TemplateDocument document = source with { Assets = [asset] };

        ValidationIssue issue = Assert.Single(
            new DocumentValidator().Validate(document),
            item => item.Code == "missing-asset-content");

        Assert.Equal(asset.Id, issue.Location.AssetId);
        Assert.True(issue.BlocksOutput);
    }

    [Fact]
    public void MissingQrCenterIconAssetPointsToBarcodeProperty()
    {
        Guid missingAssetId = Guid.NewGuid();
        BarcodeElement barcode = new()
        {
            Symbology = BarcodeSymbology.QrCode,
            CenterIconAssetId = missingAssetId,
            Content = ElementExpression.Literal("RMPP QR ICON"),
            Bounds = new MmRect(10, 10, 30, 30),
        };
        (TemplateDocument document, _) = TestDocumentFactory.Create(barcode);

        ValidationIssue issue = Assert.Single(
            new DocumentValidator().Validate(document),
            item => item.Code == "missing-qr-center-icon-asset");

        Assert.Equal(barcode.Id, issue.Location.ElementId);
        Assert.Equal(missingAssetId, issue.Location.AssetId);
        Assert.Equal(nameof(BarcodeElement.CenterIconAssetId), issue.Location.PropertyPath);
    }
}
