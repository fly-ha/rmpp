namespace Rmpp.Printing.Windows.Printers;

/// <summary>按稳定身份提供打印机目录，并在每次刷新时重新读取驱动能力。</summary>
public sealed class WindowsPrinterCatalog(
    IWindowsPrintSystemAdapter printSystem)
{
    public IReadOnlyList<WindowsPrinterCapabilities> GetPrinters(CancellationToken cancellationToken = default) =>
        printSystem.GetQueues(cancellationToken)
            .Select(Map)
            .OrderByDescending(static printer => printer.IsDefault)
            .ThenBy(static printer => printer.Identity.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    /// <summary>把可能较慢的驱动能力读取移出 UI 线程，同时保留同步接口供打印提交和测试使用。</summary>
    public Task<IReadOnlyList<WindowsPrinterCapabilities>> GetPrintersAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(() => GetPrinters(cancellationToken), cancellationToken);

    public WindowsPrinterCapabilities GetRequired(
        string stablePrinterId,
        CancellationToken cancellationToken = default) =>
        GetPrinters(cancellationToken).FirstOrDefault(
            printer => string.Equals(printer.Identity.StableId, stablePrinterId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"未找到打印机：{stablePrinterId}");

    private WindowsPrinterCapabilities Map(WindowsPrintQueueSnapshot queue) => new()
    {
        Identity = WindowsPrinterIdentityResolver.Resolve(queue),
        QueueName = queue.QueueName,
        IsDefault = queue.IsDefault,
        Media = queue.Media,
        Orientations = queue.Orientations,
        Resolutions = queue.Resolutions,
        DefaultPrintableArea = queue.DefaultPrintableArea,
        SupportsDuplex = queue.SupportsDuplex,
    };
}
