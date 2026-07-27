using Rmpp.Application.Abstractions;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Clipboard;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class ClipboardTests
{
    [Fact]
    public async Task PasteRemapsIdentityTransfersAssetAndOffsetsElement()
    {
        AssetReference asset = new(Guid.NewGuid(), "logo.png", "image/png", new string('b', 64));
        ImageElement image = new()
        {
            AssetId = asset.Id,
            Bounds = new MmRect(10, 20, 30, 15),
        };
        (TemplateDocument sourceBase, _) = TestDocumentFactory.Create(image);
        TemplateDocument source = sourceBase with { Assets = [asset] };
        DocumentSession sourceSession = new(source, assetContents: new Dictionary<Guid, ReadOnlyMemory<byte>>
        {
            [asset.Id] = new byte[] { 1, 2, 3 },
        });
        (TemplateDocument targetDocument, _) = TestDocumentFactory.Create();
        DocumentSession targetSession = new(targetDocument);
        MemoryClipboardAdapter adapter = new();
        ClipboardElementService service = new(adapter);
        await service.CopyAsync(sourceSession.State, [image.Id]);

        ClipboardPasteResult? paste = await service.PasteAsync(
            targetSession,
            new EditorCommandDispatcher(targetSession),
            new MmPoint(5, 5));

        Assert.NotNull(paste);
        ImageElement pasted = Assert.IsType<ImageElement>(Assert.Single(targetSession.State.Document.Elements));
        Assert.NotEqual(image.Id, pasted.Id);
        Assert.Equal(new MmRect(15, 25, 30, 15), pasted.Bounds);
        Assert.NotNull(pasted.AssetId);
        Assert.Contains(pasted.AssetId!.Value, targetSession.State.AssetContents.Keys);
        Assert.Contains(pasted.Id, targetSession.State.SelectedElementIds);
    }

    [Fact]
    public void ExistingHashReusesTargetAssetIdentity()
    {
        AssetReference sourceAsset = new(Guid.NewGuid(), "source.png", "image/png", new string('c', 64));
        ImageElement image = new() { AssetId = sourceAsset.Id, Bounds = new MmRect(0, 0, 10, 10) };
        ElementClipboardPayload payload = new()
        {
            SourceDocumentId = Guid.NewGuid(),
            Elements = [image],
            Assets = [new ClipboardAsset(sourceAsset, new byte[] { 4, 5 })],
        };
        AssetReference existing = new(Guid.NewGuid(), "existing.png", "image/png", sourceAsset.Sha256);
        (TemplateDocument targetBase, LayerDefinition layer) = TestDocumentFactory.Create();
        TemplateDocument targetDocument = targetBase with { Assets = [existing] };
        DocumentSession target = new(targetDocument, assetContents: new Dictionary<Guid, ReadOnlyMemory<byte>>
        {
            [existing.Id] = new byte[] { 4, 5 },
        });

        ClipboardPasteResult result = ClipboardElementService.PreparePaste(target.State, payload, new MmPoint(1, 1));
        TemplateDocument pastedDocument = result.Command.Execute(targetDocument);
        ImageElement pasted = Assert.IsType<ImageElement>(Assert.Single(pastedDocument.Elements));

        Assert.Equal(existing.Id, pasted.AssetId);
        Assert.Equal(layer.Id, pasted.LayerId);
        Assert.Empty(result.AssetsToImport);
        Assert.Single(pastedDocument.Assets);
    }

    private sealed class MemoryClipboardAdapter : IClipboardAdapter
    {
        private ElementClipboardPayload? payload;

        public Task SetElementsAsync(ElementClipboardPayload payload, CancellationToken cancellationToken = default)
        {
            this.payload = payload;
            return Task.CompletedTask;
        }

        public Task<ElementClipboardPayload?> GetElementsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(payload);
    }
}
