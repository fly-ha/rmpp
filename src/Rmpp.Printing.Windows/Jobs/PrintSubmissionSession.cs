using Rmpp.Application.Abstractions;

namespace Rmpp.Printing.Windows.Jobs;

public enum PrintSubmissionState
{
    Created,
    Submitting,
    Completed,
    Cancelled,
    Failed,
}

/// <summary>封装一次性打印提交状态；失败和取消只改变会话，不触碰文档或校准存储。</summary>
public sealed class PrintSubmissionSession(IWindowsSpoolAdapter spoolAdapter)
{
    private int started;

    public PrintSubmissionState State { get; private set; } = PrintSubmissionState.Created;
    public Exception? Failure { get; private set; }

    public async Task<PrintSubmissionResult> SubmitAsync(
        WindowsSpoolRequest request,
        IProgress<PrintSubmissionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            throw new InvalidOperationException("一个打印提交会话只能执行一次。");
        }

        State = PrintSubmissionState.Submitting;
        try
        {
            PrintSubmissionResult result = await spoolAdapter.SubmitAsync(request, progress, cancellationToken)
                .ConfigureAwait(false);
            State = result.WasCancelled ? PrintSubmissionState.Cancelled : PrintSubmissionState.Completed;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            State = PrintSubmissionState.Cancelled;
            return new PrintSubmissionResult(0, true, null);
        }
        catch (Exception exception)
        {
            Failure = exception;
            State = PrintSubmissionState.Failed;
            throw;
        }
    }
}
