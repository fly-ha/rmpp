using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;

namespace Rmpp.Application.Editing.Commands;

/// <summary>在一次可撤销文档变更中绑定、替换或清除图片资源，并清理不再被引用的资源元数据。</summary>
public sealed class SetImageAssetCommand(Guid imageElementId, AssetReference? asset) : IEditorCommand
{
    public string Description => asset is null ? "Clear image asset" : "Set image asset";

    public TemplateDocument Execute(TemplateDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ImageElement? target = document.Elements.OfType<ImageElement>().FirstOrDefault(item => item.Id == imageElementId);
        if (target is null || target.IsLocked)
        {
            return document;
        }

        if (asset is not null
            && (asset.Id == Guid.Empty || document.Assets.Any(item => item.Id == asset.Id)))
        {
            throw new InvalidOperationException("Image asset requires a unique non-empty identity.");
        }

        TemplateElement[] elements = document.Elements.Select(element =>
            element.Id == imageElementId
                ? ((ImageElement)element) with { AssetId = asset?.Id, VariablePath = null }
                : element).ToArray();
        List<AssetReference> assets = document.Assets.ToList();
        if (asset is not null)
        {
            assets.Add(asset);
        }

        if (target.AssetId is { } previousAssetId)
        {
            if (!AssetReferenceUsage.IsReferenced(document, previousAssetId, elements))
            {
                assets.RemoveAll(item => item.Id == previousAssetId);
            }
        }

        return document with { Elements = elements, Assets = assets.ToArray() };
    }
}
