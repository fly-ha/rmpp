using Rmpp.Domain.Printing;

namespace Rmpp.Application.Printing;

/// <summary>使用显式份数与排序方式冻结页面顺序，避免依赖打印驱动的逐份语义。</summary>
public sealed record PrintCopyPolicy
{
    public int Copies { get; init; } = 1;
    public PrintCopyOrder Order { get; init; } = PrintCopyOrder.PerRecord;

    public int RecordCopies => Order == PrintCopyOrder.PerRecord ? Copies : 1;
    public int JobCopies => Order == PrintCopyOrder.Collated ? Copies : 1;

    public PrintCopyPolicy Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Copies);
        return this;
    }
}
