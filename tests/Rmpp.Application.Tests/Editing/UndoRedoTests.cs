using Rmpp.Application.Abstractions;
using Rmpp.Application.Documents;
using Rmpp.Application.Editing;
using Rmpp.Application.Editing.Commands;
using Rmpp.Domain.Elements;
using Xunit;

namespace Rmpp.Application.Tests.Editing;

public sealed class UndoRedoTests
{
    [Fact]
    public void CompletedGestureProducesOneUndoEntry()
    {
        RectangleElement element = TestDocumentFactory.Rectangle();
        (Rmpp.Domain.Documents.TemplateDocument document, _) = TestDocumentFactory.Create(element);
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session);

        using (UndoTransaction transaction = dispatcher.BeginTransaction("Drag", "drag-element"))
        {
            dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0));
            dispatcher.Execute(new MoveElementsCommand([element.Id], 2, 0));
            dispatcher.Execute(new MoveElementsCommand([element.Id], 3, 0));
            transaction.Complete();
        }

        Assert.Equal(1, dispatcher.History.UndoCount);
        Assert.Equal(16, session.State.Document.Elements[0].Bounds.X);
        Assert.True(dispatcher.Undo());
        Assert.Equal(10, session.State.Document.Elements[0].Bounds.X);
    }

    [Fact]
    public void NewEditAfterUndoInvalidatesRedo()
    {
        RectangleElement element = TestDocumentFactory.Rectangle();
        (Rmpp.Domain.Documents.TemplateDocument document, _) = TestDocumentFactory.Create(element);
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session);

        dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0));
        dispatcher.Execute(new MoveElementsCommand([element.Id], 2, 0));
        Assert.True(dispatcher.Undo());
        Assert.True(dispatcher.History.CanRedo);

        dispatcher.Execute(new MoveElementsCommand([element.Id], 0, 4));

        Assert.False(dispatcher.History.CanRedo);
        Assert.False(dispatcher.Redo());
    }

    [Fact]
    public void SameCoalescingKeyWithinWindowMergesEntries()
    {
        RectangleElement element = TestDocumentFactory.Rectangle();
        (Rmpp.Domain.Documents.TemplateDocument document, _) = TestDocumentFactory.Create(element);
        FakeClock clock = new();
        DocumentSession session = new(document);
        EditorCommandDispatcher dispatcher = new(session, clock: clock);

        dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0, "nudge"));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0, "nudge"));

        Assert.Equal(1, dispatcher.History.UndoCount);
        Assert.True(dispatcher.Undo());
        Assert.Equal(10, session.State.Document.Elements[0].Bounds.X);
    }

    [Fact]
    public void HistoryDropsOldestEntriesAtConfiguredBound()
    {
        RectangleElement element = TestDocumentFactory.Rectangle();
        (Rmpp.Domain.Documents.TemplateDocument document, _) = TestDocumentFactory.Create(element);
        DocumentSession session = new(document);
        UndoRedoManager history = new(maximumEntries: 2, maximumEstimatedBytes: 10_000_000);
        EditorCommandDispatcher dispatcher = new(session, history);

        dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0));
        dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0));
        dispatcher.Execute(new MoveElementsCommand([element.Id], 1, 0));

        Assert.Equal(2, history.UndoCount);
        Assert.True(dispatcher.Undo());
        Assert.True(dispatcher.Undo());
        Assert.False(dispatcher.Undo());
        Assert.Equal(11, session.State.Document.Elements[0].Bounds.X);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset LocalNow => UtcNow.ToLocalTime();

        public void Advance(TimeSpan amount) => UtcNow += amount;
    }
}
