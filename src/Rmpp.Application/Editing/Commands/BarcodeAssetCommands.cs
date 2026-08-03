using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

/// <summary>在单次可撤销变更中绑定、替换或清除二维码中心图标，并保留仍被其他对象使用的共享资源。</summary>
public sealed class SetBarcodeCenterIconAssetCommand(Guid barcodeElementId, AssetReference? asset) : IEditorCommand
{
    public string Description => asset is null ? "Clear QR center icon" : "Set QR center icon";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        BarcodeElement? target = document.Elements.OfType<BarcodeElement>()
            .FirstOrDefault(item => item.Id == barcodeElementId);
        if (target is null || target.IsLocked)
        {
            return document;
        }
        if (target.Symbology != BarcodeSymbology.QrCode)
        {
            throw new InvalidOperationException("Center icons can be assigned only to QR codes.");
        }
        if (asset is not null
            && (asset.Id == Guid.Empty || document.Assets.Any(item => item.Id == asset.Id)))
        {
            throw new InvalidOperationException("QR center icon requires a unique non-empty asset identity.");
        }

        TemplateElement[] elements = document.Elements.Select(element =>
            element.Id == barcodeElementId
                ? ((BarcodeElement)element) with
                {
                    CenterIconAssetId = asset?.Id,
                    ErrorCorrectionLevel = asset is null ? target.ErrorCorrectionLevel : Math.Max(3, target.ErrorCorrectionLevel),
                }
                : element).ToArray();
        List<AssetReference> assets = document.Assets.ToList();
        if (asset is not null)
        {
            assets.Add(asset);
        }

        if (target.CenterIconAssetId is { } previousAssetId
            && !AssetReferenceUsage.IsReferenced(document, previousAssetId, elements))
        {
            assets.RemoveAll(item => item.Id == previousAssetId);
        }

        return document with { Elements = elements, Assets = assets.ToArray() };
    }
}

/// <summary>切换条码制式；离开二维码制式时同步清除中心图标，避免产生不可见的无效配置。</summary>
public sealed class SetBarcodeSymbologyCommand(Guid barcodeElementId, BarcodeSymbology symbology) : IEditorCommand
{
    public string Description => "Set barcode symbology";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        BarcodeElement? target = document.Elements.OfType<BarcodeElement>()
            .FirstOrDefault(item => item.Id == barcodeElementId);
        if (target is null || target.IsLocked || target.Symbology == symbology)
        {
            return document;
        }

        Guid? discardedAssetId = symbology == BarcodeSymbology.QrCode ? null : target.CenterIconAssetId;
        TemplateElement[] elements = document.Elements.Select(element =>
            element.Id == barcodeElementId
                ? ((BarcodeElement)element) with
                {
                    Symbology = symbology,
                    CenterIconAssetId = discardedAssetId.HasValue ? null : target.CenterIconAssetId,
                }
                : element).ToArray();
        AssetReference[] assets = discardedAssetId is { } assetId
            && !AssetReferenceUsage.IsReferenced(document, assetId, elements)
                ? document.Assets.Where(item => item.Id != assetId).ToArray()
                : document.Assets.ToArray();
        return document with { Elements = elements, Assets = assets };
    }
}
