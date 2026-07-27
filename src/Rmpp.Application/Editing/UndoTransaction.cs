namespace Rmpp.Application.Editing;

/// <summary>把连续拖拽或批量属性更新合并为一个逻辑撤销项。</summary>
public sealed class UndoTransaction(EditorCommandDispatcher dispatcher) : IDisposable
{
    private bool finished;

    public void Complete()
    {
        if (finished)
        {
            throw new InvalidOperationException("The editor transaction has already finished.");
        }

        dispatcher.CompleteTransaction();
        finished = true;
    }

    public void Cancel()
    {
        if (finished)
        {
            return;
        }

        dispatcher.CancelTransaction();
        finished = true;
    }

    public void Dispose() => Cancel();
}
