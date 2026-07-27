using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Documents;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class LayerGuideCommandTests
{
    [Fact]
    public void LayerAndGuideChangesParticipateInUndoRedo()
    {
        TemplateDocument document = TemplateDocument.CreateNew("test");
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session);
        LayerDefinition layer = document.Layers[0];

        dispatcher.Execute(new ChangeLayerPropertiesCommand(layer.Id, current => current with { IsVisible = false }));
        dispatcher.Execute(new ChangeGuidesCommand([new GuideDefinition(Guid.NewGuid(), GuideOrientation.Vertical, 25)]));

        Assert.False(session.State.Document.Layers[0].IsVisible);
        Assert.Single(session.State.Document.Guides);
        Assert.True(dispatcher.Undo());
        Assert.Empty(session.State.Document.Guides);
        Assert.True(dispatcher.Undo());
        Assert.True(session.State.Document.Layers[0].IsVisible);
    }
}
