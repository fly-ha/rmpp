using System.Printing;
using System.Threading;
using System.Windows;
using System.Windows.Xps;
using Rmpp.Application.Abstractions;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Printing.Windows.Rendering;

namespace Rmpp.Printing.Windows.Jobs;

/// <summary>在专用 STA 线程通过 WPF/XPS 提交；取消可停止未请求页面，已进入 spooler 的页仍可能打印。</summary>
public sealed class WpfXpsSpoolAdapter(WpfPrintSceneRenderer? renderer = null) : IWindowsSpoolAdapter
{
    private readonly WpfPrintSceneRenderer renderer = renderer ?? new();

    public Task<PrintSubmissionResult> SubmitAsync(
        WindowsSpoolRequest request,
        IProgress<PrintSubmissionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TaskCompletionSource<PrintSubmissionResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() => SubmitOnSta(request, progress, completion, cancellationToken))
        {
            IsBackground = true,
            Name = "RMPP Windows Print Submission",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private void SubmitOnSta(
        WindowsSpoolRequest request,
        IProgress<PrintSubmissionProgress>? progress,
        TaskCompletionSource<PrintSubmissionResult> completion,
        CancellationToken cancellationToken)
    {
        int submittedPages = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using LocalPrintServer server = new();
            using PrintQueue queue = server.GetPrintQueue(request.QueueName);
            queue.CurrentJobSettings.Description = request.JobName;
            PrintTicket requestedTicket = PrintTicketFactory.ToNative(request.Ticket);
            ValidationResult validation = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, requestedTicket);
            if (validation.ConflictStatus != ConflictStatus.NoConflict)
            {
                throw new InvalidOperationException("打印机驱动拒绝了所选介质、方向、分辨率或份数。");
            }

            Size pageSize = new(
                PrintableAreaService.ToDeviceIndependentPixels(request.Ticket.Media.Size.Width),
                PrintableAreaService.ToDeviceIndependentPixels(request.Ticket.Media.Size.Height));
            Progress<PrintSubmissionProgress> countingProgress = new(value =>
            {
                submittedPages = Math.Max(submittedPages, value.SubmittedPages);
                progress?.Report(value);
            });
            using StreamingDocumentPaginator paginator = new(
                request.Scenes,
                renderer,
                request.Calibration,
                pageSize,
                countingProgress,
                cancellationToken,
                request.AssetProvider,
                Math.Max(request.Ticket.Resolution.DpiX, request.Ticket.Resolution.DpiY));
            XpsDocumentWriter writer = PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(paginator, validation.ValidatedPrintTicket);
            completion.TrySetResult(new PrintSubmissionResult(submittedPages, false, null));
        }
        catch (OperationCanceledException)
        {
            completion.TrySetResult(new PrintSubmissionResult(submittedPages, true, null));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
