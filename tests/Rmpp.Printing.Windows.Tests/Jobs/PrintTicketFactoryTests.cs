using Rmpp.Application.Abstractions;
using Rmpp.Printing.Windows.Jobs;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Printing.Windows.Tests.Printers;
using Rmpp.Rendering.Scene;
using Rmpp.Domain.Geometry;
using Xunit;

namespace Rmpp.Printing.Windows.Tests.Jobs;

public sealed class PrintTicketFactoryTests
{
    [Fact]
    public void MatchesExplicitMediaOrientationResolutionAndCopies()
    {
        WindowsPrintQueueSnapshot queue = WindowsPrinterCatalogTests.CreateQueue("打印机", "PORT1");
        WindowsPrinterCapabilities capabilities = new WindowsPrinterCatalog(
            new WindowsPrinterCatalogTests.FakePrintSystem([queue])).GetPrinters().Single();
        PrintSubmissionRequest request = CreateRequest() with
        {
            Orientation = PrintMediaOrientation.Landscape,
            ResolutionDpi = 600,
            Copies = 3,
        };

        WindowsPrintTicketDefinition ticket = PrintTicketFactory.Create(request, capabilities);

        Assert.Equal("a4", ticket.Media.Key);
        Assert.Equal(PrintMediaOrientation.Landscape, ticket.Orientation);
        Assert.Equal(600, ticket.Resolution.DpiX);
        Assert.Equal(3, ticket.Copies);
    }

    [Fact]
    public void RejectsUnsupportedTicketAndFitToPage()
    {
        WindowsPrintQueueSnapshot queue = WindowsPrinterCatalogTests.CreateQueue("打印机", "PORT1");
        WindowsPrinterCapabilities capabilities = new WindowsPrinterCatalog(
            new WindowsPrinterCatalogTests.FakePrintSystem([queue])).GetPrinters().Single();

        Assert.Throws<InvalidOperationException>(() =>
            PrintTicketFactory.Create(CreateRequest() with { ResolutionDpi = 1200 }, capabilities));
        Assert.Throws<NotSupportedException>(() =>
            PrintTicketFactory.Create(CreateRequest() with { AllowFitToPageScaling = true }, capabilities));
    }

    [Fact]
    public void CreatesExplicitCustomMediaWhenDriverDoesNotListTemplateSize()
    {
        WindowsPrintQueueSnapshot queue = WindowsPrinterCatalogTests.CreateQueue("打印机", "PORT1");
        WindowsPrinterCapabilities capabilities = new WindowsPrinterCatalog(
            new WindowsPrinterCatalogTests.FakePrintSystem([queue])).GetPrinters().Single();

        WindowsPrintTicketDefinition ticket = PrintTicketFactory.Create(CreateRequest() with
        {
            MediaName = "custom:180x120",
            CustomMediaSize = new MmSize(180, 120),
        }, capabilities);

        Assert.True(ticket.Media.IsCustom);
        Assert.Equal(new MmSize(180, 120), ticket.Media.Size);
        Assert.Equal(180 / 25.4 * 96, PrintTicketFactory.ToNative(ticket).PageMediaSize!.Width!.Value, 6);
    }

    internal static PrintSubmissionRequest CreateRequest() => new()
    {
        PrinterId = "printer",
        MediaName = "a4",
        Scenes = EmptyScenes(),
        ResolutionDpi = 300,
    };

    private static async IAsyncEnumerable<RenderScene> EmptyScenes()
    {
        await Task.CompletedTask;
        yield break;
    }
}
