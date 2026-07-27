using Rmpp.Application.Abstractions;
using Rmpp.Application.Documents;
using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing;

/// <summary>在文档会话上执行命令，并统一管理事务、撤销、重做和只读保护。</summary>
public sealed class EditorCommandDispatcher
{
    private readonly DocumentSession session;
    private readonly UndoRedoManager history;
    private readonly IClock clock;
    private TransactionState? transaction;

    public EditorCommandDispatcher(
        DocumentSession session,
        UndoRedoManager? history = null,
        IClock? clock = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.history = history ?? new UndoRedoManager();
        this.clock = clock ?? new SystemClock();
    }

    public UndoRedoManager History => history;

    public bool Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        TemplateDocument before = session.State.Document;
        Guid beforeRevision = session.State.CurrentRevision;
        TemplateDocument after = command.Execute(before);
        if (ReferenceEquals(before, after))
        {
            return false;
        }

        Guid afterRevision = session.ApplyDocument(after);
        if (transaction is null)
        {
            history.Record(
                before,
                beforeRevision,
                after,
                afterRevision,
                command.Description,
                command.CoalescingKey,
                clock.UtcNow);
        }
        else
        {
            transaction = transaction with
            {
                After = after,
                AfterRevision = afterRevision,
                HasChanges = true,
            };
        }

        return true;
    }

    public UndoTransaction BeginTransaction(string description, string? coalescingKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (transaction is not null)
        {
            throw new InvalidOperationException("Nested editor transactions are not supported.");
        }

        transaction = new TransactionState(
            session.State.Document,
            session.State.CurrentRevision,
            session.State.Document,
            session.State.CurrentRevision,
            description,
            coalescingKey,
            false);
        return new UndoTransaction(this);
    }

    public bool Undo()
    {
        EnsureNoTransaction();
        EditorHistorySnapshot? snapshot = history.Undo();
        if (snapshot is null)
        {
            return false;
        }

        session.ApplyDocument(snapshot.Document, snapshot.Revision);
        return true;
    }

    public bool Redo()
    {
        EnsureNoTransaction();
        EditorHistorySnapshot? snapshot = history.Redo();
        if (snapshot is null)
        {
            return false;
        }

        session.ApplyDocument(snapshot.Document, snapshot.Revision);
        return true;
    }

    internal void CompleteTransaction()
    {
        TransactionState active = transaction
            ?? throw new InvalidOperationException("No editor transaction is active.");
        transaction = null;
        if (!active.HasChanges)
        {
            return;
        }

        history.Record(
            active.Before,
            active.BeforeRevision,
            active.After,
            active.AfterRevision,
            active.Description,
            active.CoalescingKey,
            clock.UtcNow);
    }

    internal void CancelTransaction()
    {
        TransactionState active = transaction
            ?? throw new InvalidOperationException("No editor transaction is active.");
        transaction = null;
        if (active.HasChanges)
        {
            session.ApplyDocument(active.Before, active.BeforeRevision);
        }
    }

    private void EnsureNoTransaction()
    {
        if (transaction is not null)
        {
            throw new InvalidOperationException("Complete or cancel the active editor transaction first.");
        }
    }

    private sealed record TransactionState(
        TemplateDocument Before,
        Guid BeforeRevision,
        TemplateDocument After,
        Guid AfterRevision,
        string Description,
        string? CoalescingKey,
        bool HasChanges);
}
