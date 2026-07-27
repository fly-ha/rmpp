using Rmpp.Application.Printing;
using Rmpp.Rendering.Layout;
using Xunit;

namespace Rmpp.Application.Tests.Printing;

public sealed class PrintPreviewTargetTests
{
    [Fact]
    public async Task PreviewAndPdfUseSamePlanButDifferentTargetCacheEntries()
    {
        PrintJobPlan plan = new PrintJobPlanner().Plan(new PrintJobRequest { Document = TestDocumentFactory.Create().Document });
        PrintPreviewService service = new(maximumCachedPages: 4);

        var preview = await service.GetPageAsync(plan, 0, RenderTarget.Preview);
        var pdf = await service.GetPageAsync(plan, 0, RenderTarget.Pdf);
        var previewAgain = await service.GetPageAsync(plan, 0, RenderTarget.Preview);

        Assert.Same(preview, previewAgain);
        Assert.NotSame(preview, pdf);
        Assert.Equal(2, service.BuiltPageCount);
        Assert.Equal(2, service.CachedPageCount);
    }
}
