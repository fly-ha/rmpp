namespace Rmpp.Application.Printing;

/// <summary>区分每条记录副本和整个任务副本，避免依赖打印驱动隐式副本语义。</summary>
public sealed record PrintCopyPolicy
{
    public int RecordCopies { get; init; } = 1;
    public int JobCopies { get; init; } = 1;

    public PrintCopyPolicy Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RecordCopies);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(JobCopies);
        return this;
    }
}
