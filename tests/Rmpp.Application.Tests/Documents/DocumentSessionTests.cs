using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Elements;
using Xunit;

namespace Rmpp.Application.Tests.Documents;

public sealed class DocumentSessionTests
{
    [Fact]
    public void SessionsKeepDocumentsSelectionsAndHistoryIsolated()
    {
        RectangleElement first = TestDocumentFactory.Rectangle();
        RectangleElement second = TestDocumentFactory.Rectangle(x: 50);
        (Rmpp.Domain.Documents.TemplateDocument firstDocument, _) = TestDocumentFactory.Create(first);
        (Rmpp.Domain.Documents.TemplateDocument secondDocument, _) = TestDocumentFactory.Create(second);
        DocumentSession firstSession = new(firstDocument);
        DocumentSession secondSession = new(secondDocument);
        EditorCommandDispatcher dispatcher = new(firstSession);

        firstSession.SetSelection([first.Id]);
        dispatcher.Execute(new MoveElementsCommand([first.Id], 5, 3));

        Assert.True(firstSession.State.IsDirty);
        Assert.False(secondSession.State.IsDirty);
        Assert.Equal(15, firstSession.State.Document.Elements[0].Bounds.X);
        Assert.Equal(50, secondSession.State.Document.Elements[0].Bounds.X);
        Assert.Empty(secondSession.State.SelectedElementIds);
    }

    [Fact]
    public void UndoBackToSavedRevisionClearsDirtyState()
    {
        RectangleElement element = TestDocumentFactory.Rectangle();
        (Rmpp.Domain.Documents.TemplateDocument document, _) = TestDocumentFactory.Create(element);
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session);

        dispatcher.Execute(new MoveElementsCommand([element.Id], 5, 0));
        session.MarkSaved("saved.rmpp");
        dispatcher.Execute(new MoveElementsCommand([element.Id], 2, 0));

        Assert.True(session.State.IsDirty);
        Assert.True(dispatcher.Undo());
        Assert.False(session.State.IsDirty);
        Assert.Equal("saved.rmpp", session.State.FilePath);
    }

    [Fact]
    public void SelectionDoesNotChangeDirtyState()
    {
        RectangleElement element = TestDocumentFactory.Rectangle();
        (Rmpp.Domain.Documents.TemplateDocument document, _) = TestDocumentFactory.Create(element);
        DocumentSession session = new(document);

        session.SetSelection([element.Id]);

        Assert.False(session.State.IsDirty);
        Assert.Contains(element.Id, session.State.SelectedElementIds);
    }
}
