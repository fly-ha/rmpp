using System.Diagnostics;
using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Printing.Windows.Jobs;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Printing.Windows.Tests.Printers;
using Rmpp.Rendering.Scene;
using Xunit;

namespace Rmpp.Printing.Windows.Tests.Performance;

public sealed class PrintStreamingPerformanceTests
{
    [Fact, Trait("Category", "Performance")]
    public async Task StreamsTwoThousandScenesWithoutMaterializingJob()
    {
        WindowsPrintQueueSnapshot queue = WindowsPrinterCatalogTests.CreateQueue("性能打印机", "PORT1");
        CountingSpoolAdapter spool = new();
        WindowsPrintService service = new(new WindowsPrinterCatalog(new WindowsPrinterCatalogTests.FakePrintSystem([queue])), spool);
        WindowsMediaDefinition media = queue.Media[0];
        Stopwatch watch = Stopwatch.StartNew();
        PrintSubmissionResult result = await service.SubmitAsync(new PrintSubmissionRequest
        {
            PrinterId = new WindowsPrinterCatalog(new WindowsPrinterCatalogTests.FakePrintSystem([queue])).GetPrinters()[0].Identity.StableId,
            MediaName = media.Key,
            Scenes = Scenes(2_000, media.Size),
            ResolutionDpi = queue.Resolutions[0].DpiX,
        });
        watch.Stop();
        Assert.Equal(2_000, result.SubmittedPages);
        Assert.Equal(2_000, spool.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), $"流式提交耗时 {watch.Elapsed}");
    }

    private static async IAsyncEnumerable<RenderScene> Scenes(int count, MmSize size)
    {
        for (int index = 0; index < count; index++)
        {
            yield return new RenderScene
            {
                DocumentId = Guid.NewGuid(),
                Pages = [new RenderPage { PageNumber = index + 1, Size = size, Clip = RenderClip.FromRectangle(new MmRect(0, 0, size.Width, size.Height)), Commands = Array.Empty<RenderCommand>() }],
            };
            await Task.Yield();
        }
    }

    private sealed class CountingSpoolAdapter : IWindowsSpoolAdapter
    {
        public int Count { get; private set; }
        public async Task<PrintSubmissionResult> SubmitAsync(WindowsSpoolRequest request, IProgress<PrintSubmissionProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            await foreach (RenderScene _ in request.Scenes.WithCancellation(cancellationToken)) Count++;
            return new PrintSubmissionResult(Count, false, null);
        }
    }
}
