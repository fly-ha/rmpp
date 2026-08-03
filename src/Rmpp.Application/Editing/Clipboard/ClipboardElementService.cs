using Rmpp.Application.Abstractions;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

namespace Rmpp.Application.Editing.Clipboard;

/// <summary>保存粘贴命令、待导入资源和新元素身份。</summary>
public sealed record ClipboardPasteResult(
    IEditorCommand Command,
    IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> AssetsToImport,
    IReadOnlyList<Guid> PastedElementIds);

/// <summary>构造私有剪贴板负载，并在跨文档粘贴时重建身份和资源引用。</summary>
public sealed class ClipboardElementService(IClipboardAdapter clipboardAdapter)
{
    private static readonly MmPoint DefaultPasteOffset = new(5, 5);

    public static ElementClipboardPayload CreatePayload(
        DocumentSessionState state,
        IEnumerable<Guid> elementIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        HashSet<Guid> ids = elementIds?.ToHashSet() ?? throw new ArgumentNullException(nameof(elementIds));
        TemplateElement[] elements = state.Document.Elements.Where(element => ids.Contains(element.Id)).ToArray();
        HashSet<Guid> assetIds = elements
            .OfType<ImageElement>()
            .Where(static image => image.AssetId.HasValue)
            .Select(static image => image.AssetId!.Value)
            .Concat(elements
                .OfType<BarcodeElement>()
                .Where(static barcode => barcode.CenterIconAssetId.HasValue)
                .Select(static barcode => barcode.CenterIconAssetId!.Value))
            .ToHashSet();
        Dictionary<Guid, AssetReference> references = state.Document.Assets.ToDictionary(static asset => asset.Id);
        ClipboardAsset[] assets = assetIds.Select(assetId =>
        {
            if (!references.TryGetValue(assetId, out AssetReference? reference)
                || !state.AssetContents.TryGetValue(assetId, out ReadOnlyMemory<byte> content)
                || content.IsEmpty)
            {
                throw new InvalidOperationException("Selected element references unavailable asset content.");
            }

            return new ClipboardAsset(reference, content.ToArray());
        }).ToArray();
        return new ElementClipboardPayload
        {
            SourceDocumentId = state.Document.Id,
            Elements = elements,
            Assets = assets,
        };
    }

    public Task CopyAsync(
        DocumentSessionState state,
        IEnumerable<Guid> elementIds,
        CancellationToken cancellationToken = default) =>
        clipboardAdapter.SetElementsAsync(CreatePayload(state, elementIds), cancellationToken);

    public async Task<ClipboardPasteResult?> PasteAsync(
        DocumentSession session,
        EditorCommandDispatcher dispatcher,
        MmPoint? offset = null,
        CancellationToken cancellationToken = default)
    {
        ElementClipboardPayload? payload = await clipboardAdapter.GetElementsAsync(cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        ClipboardPasteResult result = PreparePaste(session.State, payload, offset ?? DefaultPasteOffset);
        session.ImportAssets(result.AssetsToImport);
        dispatcher.Execute(result.Command);
        session.SetSelection(result.PastedElementIds);
        return result;
    }

    public static ClipboardPasteResult PreparePaste(
        DocumentSessionState target,
        ElementClipboardPayload payload,
        MmPoint? offset = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Version != ElementClipboardPayload.CurrentVersion)
        {
            throw new InvalidOperationException("Clipboard payload version is not supported.");
        }

        MmPoint effectiveOffset = offset ?? DefaultPasteOffset;
        Dictionary<Guid, Guid> assetIdMap = [];
        List<AssetReference> newReferences = [];
        Dictionary<Guid, ReadOnlyMemory<byte>> assetsToImport = [];
        foreach (ClipboardAsset asset in payload.Assets)
        {
            AssetReference? sameContent = target.Document.Assets.FirstOrDefault(existing =>
                StringComparer.OrdinalIgnoreCase.Equals(existing.Sha256, asset.Reference.Sha256)
                && StringComparer.OrdinalIgnoreCase.Equals(existing.MediaType, asset.Reference.MediaType));
            if (sameContent is not null)
            {
                assetIdMap[asset.Reference.Id] = sameContent.Id;
                continue;
            }

            Guid newId = target.Document.Assets.Any(existing => existing.Id == asset.Reference.Id)
                ? Guid.NewGuid()
                : asset.Reference.Id;
            AssetReference newReference = new(
                newId,
                asset.Reference.FileName,
                asset.Reference.MediaType,
                asset.Reference.Sha256);
            assetIdMap[asset.Reference.Id] = newId;
            newReferences.Add(newReference);
            assetsToImport[newId] = asset.Content.ToArray();
        }

        Guid fallbackLayerId = target.Document.Layers.Count > 0
            ? target.Document.Layers[0].Id
            : throw new InvalidOperationException("Target document requires at least one layer.");
        HashSet<Guid> targetLayers = target.Document.Layers.Select(static layer => layer.Id).ToHashSet();
        int zIndex = target.Document.Elements.Count == 0
            ? 0
            : target.Document.Elements.Max(static element => element.ZIndex) + 1;
        List<Guid> pastedIds = [];
        TemplateElement[] pasted = payload.Elements.Select((element, index) =>
        {
            Guid newId = Guid.NewGuid();
            pastedIds.Add(newId);
            Guid layerId = targetLayers.Contains(element.LayerId) ? element.LayerId : fallbackLayerId;
            MmRect bounds = element.Bounds.Translate(effectiveOffset.X, effectiveOffset.Y);
            return element switch
            {
                ImageElement image => image with
                {
                    Id = newId,
                    LayerId = layerId,
                    ZIndex = zIndex + index,
                    Bounds = bounds,
                    AssetId = image.AssetId is Guid sourceAssetId && assetIdMap.TryGetValue(sourceAssetId, out Guid mapped)
                        ? mapped
                        : image.AssetId,
                },
                BarcodeElement barcode => barcode with
                {
                    Id = newId,
                    LayerId = layerId,
                    ZIndex = zIndex + index,
                    Bounds = bounds,
                    CenterIconAssetId = barcode.CenterIconAssetId is Guid sourceAssetId
                        && assetIdMap.TryGetValue(sourceAssetId, out Guid mapped)
                            ? mapped
                            : barcode.CenterIconAssetId,
                },
                _ => element with { Id = newId, LayerId = layerId, ZIndex = zIndex + index, Bounds = bounds },
            };
        }).ToArray();
        return new ClipboardPasteResult(
            new PasteElementsCommand(pasted, newReferences),
            assetsToImport,
            pastedIds);
    }

    private sealed class PasteElementsCommand(
        IReadOnlyList<TemplateElement> elements,
        IReadOnlyList<AssetReference> assets) : IEditorCommand
    {
        public string Description => "Paste elements";

        public TemplateDocument Execute(TemplateDocument document)
        {
            TemplateDocument withAssets = assets.Count == 0
                ? document
                : document with { Assets = document.Assets.Concat(assets).ToArray() };
            return new AddElementsCommand(elements).Execute(withAssets);
        }
    }
}
