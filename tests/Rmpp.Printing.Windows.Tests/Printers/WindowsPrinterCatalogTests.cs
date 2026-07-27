using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Printing.Windows.Printers;
using Xunit;

namespace Rmpp.Printing.Windows.Tests.Printers;

public sealed class WindowsPrinterCatalogTests
{
    [Fact]
    public void StableIdentityIgnoresDisplayNameButChangesWithPort()
    {
        WindowsPrintQueueSnapshot first = CreateQueue("打印机 A", "PORT1");
        WindowsPrintQueueSnapshot renamed = first with { DisplayName = "办公室打印机" };
        WindowsPrintQueueSnapshot moved = first with { PortName = "PORT2" };

        Assert.Equal(
            WindowsPrinterIdentityResolver.Resolve(first).StableId,
            WindowsPrinterIdentityResolver.Resolve(renamed).StableId);
        Assert.NotEqual(
            WindowsPrinterIdentityResolver.Resolve(first).StableId,
            WindowsPrinterIdentityResolver.Resolve(moved).StableId);
    }

    [Fact]
    public void SelectingAnotherPrinterReturnsItsOwnMediaAndPrintableArea()
    {
        WindowsPrintQueueSnapshot first = CreateQueue("激光", "PORT1");
        WindowsPrintQueueSnapshot second = CreateQueue("标签", "PORT2") with
        {
            QueueName = "LabelQueue",
            Media = [CreateMedia("roll", new MmSize(60, 40), new MmRect(1, 1, 58, 38))],
        };
        WindowsPrinterCatalog catalog = new(new FakePrintSystem([first, second]));

        string secondId = WindowsPrinterIdentityResolver.Resolve(second).StableId;
        WindowsPrinterCapabilities selected = catalog.GetRequired(secondId);

        Assert.Equal("LabelQueue", selected.QueueName);
        WindowsMediaDefinition media = Assert.Single(selected.Media);
        Assert.Equal("roll", media.Key);
        Assert.Equal(new MmRect(1, 1, 58, 38), media.PrintableArea);
    }

    [Fact]
    public void PrintableAreaIsConvertedAndClampedToMedia()
    {
        MmRect result = PrintableAreaService.FromDeviceIndependentPixels(
            -10,
            9.6,
            10_000,
            9_600,
            new MmSize(100, 50));

        Assert.Equal(0, result.X);
        Assert.Equal(2.54, result.Y, 6);
        Assert.Equal(100, result.Width);
        Assert.Equal(47.46, result.Height, 6);
    }

    internal static WindowsPrintQueueSnapshot CreateQueue(string displayName, string portName) => new()
    {
        ServerName = "localhost",
        QueueName = "OfficeQueue",
        DisplayName = displayName,
        DriverName = "Generic Driver",
        PortName = portName,
        IsDefault = true,
        Media = [CreateMedia("a4", new MmSize(210, 297), new MmRect(5, 5, 200, 287))],
        Orientations = new HashSet<PrintMediaOrientation>
        {
            PrintMediaOrientation.Portrait,
            PrintMediaOrientation.Landscape,
        },
        Resolutions = [new WindowsPrintResolution(300, 300), new WindowsPrintResolution(600, 600)],
        DefaultPrintableArea = new MmRect(5, 5, 200, 287),
    };

    internal static WindowsMediaDefinition CreateMedia(string key, MmSize size, MmRect area) => new()
    {
        Key = key,
        DisplayName = key,
        DriverName = key,
        Size = size,
        PrintableArea = area,
    };

    internal sealed class FakePrintSystem(IReadOnlyList<WindowsPrintQueueSnapshot> queues) : IWindowsPrintSystemAdapter
    {
        public IReadOnlyList<WindowsPrintQueueSnapshot> GetQueues(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return queues;
        }
    }
}
