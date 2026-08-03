using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class BarcodeAssetCommandTests
{
    [Fact]
    public void SetReplaceClearAndSymbologyChangePreserveUndoAndSharedReferences()
    {
        TemplateDocument source = TemplateDocument.CreateNew("二维码图标命令");
        Guid layerId = source.Layers[0].Id;
        AssetReference shared = new(Guid.NewGuid(), "shared.png", "image/png", new string('a', 64));
        BarcodeElement barcode = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(0, 0, 30, 30),
            Symbology = BarcodeSymbology.QrCode,
            CenterIconAssetId = shared.Id,
        };
        ImageElement image = new()
        {
            LayerId = layerId,
            Bounds = new MmRect(35, 0, 20, 20),
            AssetId = shared.Id,
        };
        TemplateDocument document = source with { Assets = [shared], Elements = [barcode, image] };
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session);
        AssetReference replacement = new(Guid.NewGuid(), "replacement.png", "image/png", new string('b', 64));

        Assert.True(dispatcher.Execute(new SetBarcodeCenterIconAssetCommand(barcode.Id, replacement)));
        BarcodeElement replaced = Assert.IsType<BarcodeElement>(session.State.Document.Elements[0]);
        Assert.Equal(replacement.Id, replaced.CenterIconAssetId);
        Assert.Equal(3, replaced.ErrorCorrectionLevel);
        Assert.Contains(session.State.Document.Assets, item => item.Id == shared.Id);

        Assert.True(dispatcher.Execute(new SetBarcodeSymbologyCommand(barcode.Id, BarcodeSymbology.Code128)));
        BarcodeElement changed = Assert.IsType<BarcodeElement>(session.State.Document.Elements[0]);
        Assert.Null(changed.CenterIconAssetId);
        Assert.DoesNotContain(session.State.Document.Assets, item => item.Id == replacement.Id);

        Assert.True(dispatcher.Undo());
        Assert.Equal(replacement.Id, Assert.IsType<BarcodeElement>(session.State.Document.Elements[0]).CenterIconAssetId);
        Assert.Contains(session.State.Document.Assets, item => item.Id == replacement.Id);
    }
}
