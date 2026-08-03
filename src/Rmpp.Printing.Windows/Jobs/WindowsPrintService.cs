using Rmpp.Application.Abstractions;
using Rmpp.Domain.Printing;
using Rmpp.Printing.Windows.Printers;

namespace Rmpp.Printing.Windows.Jobs;

/// <summary>应用打印端口的 Windows 实现：重新读取能力、冻结票据和校准后再创建一次性提交会话。</summary>
public sealed class WindowsPrintService(
    WindowsPrinterCatalog catalog,
    IWindowsSpoolAdapter spoolAdapter,
    ICalibrationProfileProvider? calibrationProvider = null) : IPrinterService
{
    public async Task<PrintSubmissionResult> SubmitAsync(
        PrintSubmissionRequest request,
        IProgress<PrintSubmissionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        WindowsPrinterCapabilities capabilities = catalog.GetRequired(request.PrinterId, cancellationToken);
        WindowsPrintTicketDefinition ticket = PrintTicketFactory.Create(request, capabilities);
        CalibrationProfile? calibration = calibrationProvider is null
            ? null
            : await calibrationProvider.GetAsync(
                new PrinterMediaKey(capabilities.Identity.StableId, ticket.Media.Key),
                cancellationToken).ConfigureAwait(false);
        WindowsSpoolRequest spoolRequest = new()
        {
            QueueName = capabilities.QueueName,
            JobName = request.JobName,
            Ticket = ticket,
            Scenes = request.Scenes,
            AssetProvider = request.AssetProvider,
            Calibration = calibration,
        };
        PrintSubmissionSession session = new(spoolAdapter);
        return await session.SubmitAsync(spoolRequest, progress, cancellationToken).ConfigureAwait(false);
    }
}
