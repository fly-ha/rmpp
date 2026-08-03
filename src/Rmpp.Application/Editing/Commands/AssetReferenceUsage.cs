using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

/// <summary>统一判断包内资源是否仍被背景、图片元素或二维码中心图标引用，避免清理共享资源时留下断链。</summary>
internal static class AssetReferenceUsage
{
    public static bool IsReferenced(
        TemplateDocument document,
        Guid assetId,
        IReadOnlyList<TemplateElement>? elements = null,
        IReadOnlyList<BackgroundDefinition>? backgrounds = null)
    {
        IReadOnlyList<TemplateElement> effectiveElements = elements ?? document.Elements;
        IReadOnlyList<BackgroundDefinition> effectiveBackgrounds = backgrounds ?? document.Backgrounds;
        return effectiveBackgrounds.Any(background => background.AssetId == assetId)
            || effectiveElements.OfType<ImageElement>().Any(image => image.AssetId == assetId)
            || effectiveElements.OfType<BarcodeElement>().Any(barcode => barcode.CenterIconAssetId == assetId);
    }
}
