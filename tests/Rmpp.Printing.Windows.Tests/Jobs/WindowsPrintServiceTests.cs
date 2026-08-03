using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;
using Rmpp.Printing.Windows.Jobs;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Printing.Windows.Tests.Printers;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Images;
using Rmpp.Rendering.Skia;
using Xunit;

namespace Rmpp.Printing.Windows.Tests.Jobs;

public sealed class WindowsPrintServiceTests
{
    [Fact]
    public async Task PassesFrozenTicketScenesAndMatchingCalibrationToSpooler()
    {
        WindowsPrintQueueSnapshot queue = WindowsPrinterCatalogTests.CreateQueue("打印机", "PORT1");
        WindowsPrinterCatalog catalog = new(new WindowsPrinterCatalogTests.FakePrintSystem([queue]));
        string printerId = WindowsPrinterIdentityResolver.Resolve(queue).StableId;
        RenderScene scene = CreateScene();
        CapturingSpoolAdapter spool = new();
        CalibrationProfile calibration = new() { Key = new PrinterMediaKey(printerId, "a4") };
        WindowsPrintService service = new(catalog, spool, new FakeCalibrationProvider(calibration));
        IRenderAssetProvider assetProvider = new EmptyAssetProvider();
        PrintSubmissionRequest request = new()
        {
            PrinterId = printerId,
            MediaName = "a4",
            Scenes = Yield(scene),
            ResolutionDpi = 300,
            JobName = "一致性测试",
            AssetProvider = assetProvider,
        };

        PrintSubmissionResult result = await service.SubmitAsync(request);

        Assert.Equal(1, result.SubmittedPages);
        Assert.NotNull(spool.Request);
        Assert.Same(calibration, spool.Request.Calibration);
        Assert.Same(assetProvider, spool.Request.AssetProvider);
        Assert.Equal("一致性测试", spool.Request.JobName);
        Assert.Same(scene, Assert.Single(spool.ReceivedScenes));
    }

    [Fact]
    public async Task FailureLeavesSourceSceneUnchangedAndMarksSessionFailed()
    {
        RenderScene scene = CreateScene();
        FailingSpoolAdapter spool = new();
        PrintSubmissionSession session = new(spool);
        WindowsSpoolRequest request = new()
        {
            QueueName = "queue",
            JobName = "failure",
            Ticket = CreateTicket(),
            Scenes = Yield(scene),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SubmitAsync(request));

        Assert.Equal(PrintSubmissionState.Failed, session.State);
        Assert.Single(scene.Pages);
        Assert.Equal(1, scene.Pages[0].PageNumber);
    }

    [Fact]
    public async Task CancelledAdapterProducesCancelledSessionWithoutRetrying()
    {
        CancelledSpoolAdapter spool = new();
        PrintSubmissionSession session = new(spool);
        WindowsSpoolRequest request = new()
        {
            QueueName = "queue",
            JobName = "cancel",
            Ticket = CreateTicket(),
            Scenes = Yield(CreateScene()),
        };

        PrintSubmissionResult result = await session.SubmitAsync(request);

        Assert.True(result.WasCancelled);
        Assert.Equal(2, result.SubmittedPages);
        Assert.Equal(PrintSubmissionState.Cancelled, session.State);
        Assert.Equal(1, spool.CallCount);
    }

    private static WindowsPrintTicketDefinition CreateTicket() => new()
    {
        Media = WindowsPrinterCatalogTests.CreateMedia("a4", new MmSize(210, 297), new MmRect(5, 5, 200, 287)),
        Orientation = PrintMediaOrientation.Portrait,
        Resolution = new WindowsPrintResolution(300, 300),
        Copies = 1,
    };

    private static RenderScene CreateScene() => new()
    {
        DocumentId = Guid.NewGuid(),
        Pages =
        [
            new RenderPage
            {
                PageNumber = 1,
                Size = new MmSize(10, 10),
                Clip = RenderClip.FromRectangle(new MmRect(0, 0, 10, 10)),
                Commands = Array.Empty<RenderCommand>(),
            },
        ],
    };

    private static async IAsyncEnumerable<RenderScene> Yield(RenderScene scene)
    {
        await Task.Yield();
        yield return scene;
    }

    private sealed class FakeCalibrationProvider(CalibrationProfile profile) : ICalibrationProfileProvider
    {
        public ValueTask<CalibrationProfile?> GetAsync(
            PrinterMediaKey key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<CalibrationProfile?>(
                key == profile.Key ? profile : null);
        }
    }

    private sealed class CapturingSpoolAdapter : IWindowsSpoolAdapter
    {
        public WindowsSpoolRequest? Request { get; private set; }
        public List<RenderScene> ReceivedScenes { get; } = [];

        public async Task<PrintSubmissionResult> SubmitAsync(
            WindowsSpoolRequest request,
            IProgress<PrintSubmissionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            await foreach (RenderScene scene in request.Scenes.WithCancellation(cancellationToken))
            {
                ReceivedScenes.Add(scene);
            }

            return new PrintSubmissionResult(ReceivedScenes.Count, false, "fake-job");
        }
    }

    private sealed class FailingSpoolAdapter : IWindowsSpoolAdapter
    {
        public Task<PrintSubmissionResult> SubmitAsync(
            WindowsSpoolRequest request,
            IProgress<PrintSubmissionProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<PrintSubmissionResult>(new InvalidOperationException("驱动拒票"));
    }

    private sealed class CancelledSpoolAdapter : IWindowsSpoolAdapter
    {
        public int CallCount { get; private set; }

        public Task<PrintSubmissionResult> SubmitAsync(
            WindowsSpoolRequest request,
            IProgress<PrintSubmissionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new PrintSubmissionResult(2, true, null));
        }
    }

    private sealed class EmptyAssetProvider : IRenderAssetProvider
    {
        public DecodedImage? Load(RenderImage image, double targetDpi) => null;
    }
}
