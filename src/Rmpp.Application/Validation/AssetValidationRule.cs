using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Validation;

/// <summary>检查元素和背景引用的资源元数据及会话内容是否完整。</summary>
public sealed class AssetValidationRule : IDocumentValidationRule
{
    public IEnumerable<ValidationIssue> Validate(DocumentValidationContext context)
    {
        Dictionary<Guid, AssetReference> references = [];
        foreach (AssetReference asset in context.Document.Assets)
        {
            if (!references.TryAdd(asset.Id, asset))
            {
                yield return Issue("duplicate-asset-id", "Asset identity is duplicated.", context.Document.Id, asset.Id);
            }
            else if (!context.AssetContents.TryGetValue(asset.Id, out ReadOnlyMemory<byte> bytes) || bytes.IsEmpty)
            {
                yield return Issue("missing-asset-content", "Packaged asset content is unavailable.", context.Document.Id, asset.Id);
            }
        }

        foreach (ImageElement image in context.Document.Elements.OfType<ImageElement>())
        {
            if (image.AssetId is Guid assetId && !references.ContainsKey(assetId))
            {
                yield return new ValidationIssue(
                    "missing-asset-reference",
                    "Image references an asset that does not exist.",
                    ValidationSeverity.Error,
                    new ValidationLocation
                    {
                        DocumentId = context.Document.Id,
                        PageNumber = 1,
                        ElementId = image.Id,
                        AssetId = assetId,
                        PropertyPath = nameof(image.AssetId),
                    });
            }
        }

        foreach (BarcodeElement barcode in context.Document.Elements.OfType<BarcodeElement>())
        {
            if (barcode.CenterIconAssetId is Guid assetId && !references.ContainsKey(assetId))
            {
                yield return new ValidationIssue(
                    "missing-qr-center-icon-asset",
                    "QR code center icon references an asset that does not exist.",
                    ValidationSeverity.Error,
                    new ValidationLocation
                    {
                        DocumentId = context.Document.Id,
                        PageNumber = 1,
                        ElementId = barcode.Id,
                        AssetId = assetId,
                        PropertyPath = nameof(barcode.CenterIconAssetId),
                    });
            }
        }

        foreach (BackgroundDefinition background in context.Document.Backgrounds.Where(background => !references.ContainsKey(background.AssetId)))
        {
            yield return new ValidationIssue(
                "missing-background-asset",
                "Background references an asset that does not exist.",
                ValidationSeverity.Error,
                new ValidationLocation
                {
                    DocumentId = context.Document.Id,
                    PageNumber = 1,
                    BackgroundId = background.Id,
                    AssetId = background.AssetId,
                    PropertyPath = nameof(background.AssetId),
                });
        }
    }

    private static ValidationIssue Issue(string code, string message, Guid documentId, Guid assetId) =>
        new(
            code,
            message,
            ValidationSeverity.Error,
            new ValidationLocation { DocumentId = documentId, AssetId = assetId });
}
