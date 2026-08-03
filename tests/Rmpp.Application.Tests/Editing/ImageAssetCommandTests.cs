using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class ImageAssetCommandTests
{
    [Fact]
    public void ReplaceAndClearImageAssetsPreserveSharedReferencesAndUndoSnapshots()
    {
        TemplateDocument document = TemplateDocument.CreateNew("图片资源命令");
        Guid layerId = document.Layers[0].Id;
        AssetReference shared = new(Guid.NewGuid(), "shared.png", "image/png", new string('a', 64));
        ImageElement first = new() { LayerId = layerId, Bounds = new MmRect(0, 0, 20, 20), AssetId = shared.Id };
        ImageElement second = new() { LayerId = layerId, Bounds = new MmRect(25, 0, 20, 20), AssetId = shared.Id };
        document = document with { Assets = [shared], Elements = [first, second] };
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session);
        AssetReference replacement = new(Guid.NewGuid(), "replacement.jpg", "image/jpeg", new string('b', 64));

        Assert.True(dispatcher.Execute(new SetImageAssetCommand(first.Id, replacement)));
        Assert.Equal(2, session.State.Document.Assets.Count);
        Assert.Equal(replacement.Id, Assert.IsType<ImageElement>(session.State.Document.Elements[0]).AssetId);
        Assert.Contains(session.State.Document.Assets, item => item.Id == shared.Id);

        Assert.True(dispatcher.Execute(new SetImageAssetCommand(second.Id, null)));
        Assert.DoesNotContain(session.State.Document.Assets, item => item.Id == shared.Id);

        Assert.True(dispatcher.Undo());
        Assert.Contains(session.State.Document.Assets, item => item.Id == shared.Id);
        Assert.Equal(shared.Id, Assert.IsType<ImageElement>(session.State.Document.Elements[1]).AssetId);
    }
}
