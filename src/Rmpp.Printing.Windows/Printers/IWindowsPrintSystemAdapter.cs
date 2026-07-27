namespace Rmpp.Printing.Windows.Printers;

/// <summary>隔离打印机枚举和驱动能力读取，使自动化测试不依赖本机硬件。</summary>
public interface IWindowsPrintSystemAdapter
{
    IReadOnlyList<WindowsPrintQueueSnapshot> GetQueues(CancellationToken cancellationToken = default);
}
