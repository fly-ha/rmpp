using System.Printing;
using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;

namespace Rmpp.Printing.Windows.Printers;

/// <summary>读取本机 System.Printing 队列并立即投影为不可变快照，不长期持有驱动句柄。</summary>
public sealed class SystemPrintingAdapter : IWindowsPrintSystemAdapter
{
    public IReadOnlyList<WindowsPrintQueueSnapshot> GetQueues(CancellationToken cancellationToken = default)
    {
        using LocalPrintServer server = new();
        string? defaultQueueName = TryGetDefaultQueueName(server);
        List<WindowsPrintQueueSnapshot> queues = [];
        foreach (PrintQueue queue in server.GetPrintQueues())
        {
            using (queue)
            {
                cancellationToken.ThrowIfCancellationRequested();
                queues.Add(ReadQueue(queue, defaultQueueName));
            }
        }

        return queues;
    }

    private static WindowsPrintQueueSnapshot ReadQueue(PrintQueue queue, string? defaultQueueName)
    {
        queue.Refresh();
        PrintCapabilities capabilities = queue.GetPrintCapabilities();
        WindowsMediaDefinition[] media = capabilities.PageMediaSizeCapability
            .Where(static size => size.Width is > 0 && size.Height is > 0)
            .Select(size => MapMedia(size, capabilities.PageImageableArea))
            .DistinctBy(static item => item.Key, StringComparer.Ordinal)
            .ToArray();
        MmRect? defaultArea = media.Length > 0 ? media[0].PrintableArea : null;

        return new WindowsPrintQueueSnapshot
        {
            ServerName = queue.HostingPrintServer?.Name ?? string.Empty,
            QueueName = queue.Name,
            DisplayName = queue.FullName,
            DriverName = queue.QueueDriver?.Name ?? string.Empty,
            PortName = queue.QueuePort?.Name ?? string.Empty,
            IsDefault = string.Equals(queue.Name, defaultQueueName, StringComparison.OrdinalIgnoreCase),
            Media = media,
            Orientations = capabilities.PageOrientationCapability.Select(MapOrientation).ToHashSet(),
            Resolutions = capabilities.PageResolutionCapability
                .Where(static resolution => resolution.X is > 0 && resolution.Y is > 0)
                .Select(static resolution => new WindowsPrintResolution(resolution.X!.Value, resolution.Y!.Value))
                .Distinct()
                .ToArray(),
            DefaultPrintableArea = defaultArea,
            SupportsDuplex = capabilities.DuplexingCapability.Count > 0,
        };
    }

    private static WindowsMediaDefinition MapMedia(PageMediaSize media, PageImageableArea? imageableArea)
    {
        MmSize size = new(
            PrintableAreaService.ToMillimetres(media.Width!.Value),
            PrintableAreaService.ToMillimetres(media.Height!.Value));
        string driverName = media.PageMediaSizeName?.ToString() ?? "Custom";
        string key = $"{driverName.ToLowerInvariant()}:{size.Width:F3}x{size.Height:F3}";
        return new WindowsMediaDefinition
        {
            Key = key,
            DisplayName = driverName,
            DriverName = driverName,
            Size = size,
            PrintableArea = MapPrintableArea(imageableArea, size),
            IsCustom = media.PageMediaSizeName is null or PageMediaSizeName.Unknown,
        };
    }

    private static MmRect? MapPrintableArea(PageImageableArea? area, MmSize media)
    {
        if (area is null)
        {
            return null;
        }

        return PrintableAreaService.FromDeviceIndependentPixels(
            area.OriginWidth,
            area.OriginHeight,
            area.ExtentWidth,
            area.ExtentHeight,
            media);
    }

    private static PrintMediaOrientation MapOrientation(PageOrientation orientation) => orientation switch
    {
        PageOrientation.Landscape => PrintMediaOrientation.Landscape,
        PageOrientation.ReversePortrait => PrintMediaOrientation.ReversePortrait,
        PageOrientation.ReverseLandscape => PrintMediaOrientation.ReverseLandscape,
        _ => PrintMediaOrientation.Portrait,
    };

    private static string? TryGetDefaultQueueName(LocalPrintServer server)
    {
        try
        {
            using PrintQueue queue = server.DefaultPrintQueue;
            return queue.Name;
        }
        catch (PrintSystemException)
        {
            return null;
        }
    }
}
