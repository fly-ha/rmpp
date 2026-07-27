using Rmpp.Application.Abstractions;
using Rmpp.Domain.Printing;
using Rmpp.Rendering.Scene;

namespace Rmpp.Printing.Windows.Jobs;

/// <summary>交给 Windows spool 层的冻结请求；场景按需枚举，不预先保留整批全分辨率页面。</summary>
public sealed record WindowsSpoolRequest
{
    public required string QueueName { get; init; }
    public required string JobName { get; init; }
    public required WindowsPrintTicketDefinition Ticket { get; init; }
    public required IAsyncEnumerable<RenderScene> Scenes { get; init; }
    public CalibrationProfile? Calibration { get; init; }
}

public interface IWindowsSpoolAdapter
{
    Task<PrintSubmissionResult> SubmitAsync(
        WindowsSpoolRequest request,
        IProgress<PrintSubmissionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>按稳定打印机/介质键读取本机校准；实现不得修改模板。</summary>
public interface ICalibrationProfileProvider
{
    ValueTask<CalibrationProfile?> GetAsync(
        PrinterMediaKey key,
        CancellationToken cancellationToken = default);
}
