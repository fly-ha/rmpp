using System.Printing;
using Rmpp.Application.Abstractions;
using Rmpp.Printing.Windows.Printers;

namespace Rmpp.Printing.Windows.Jobs;

/// <summary>已与驱动快照匹配的明确打印票据，不包含适页缩放。</summary>
public sealed record WindowsPrintTicketDefinition
{
    public required WindowsMediaDefinition Media { get; init; }
    public required PrintMediaOrientation Orientation { get; init; }
    public required WindowsPrintResolution Resolution { get; init; }
    public int Copies { get; init; }
}

/// <summary>严格匹配介质、方向和 DPI；驱动不支持时在进入 spooler 前失败。</summary>
public sealed class PrintTicketFactory
{
    public static WindowsPrintTicketDefinition Create(
        PrintSubmissionRequest request,
        WindowsPrinterCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capabilities);
        if (request.AllowFitToPageScaling)
        {
            throw new NotSupportedException("首版打印后端不提供适页缩放；请使用 100% 物理尺寸。");
        }

        if (request.Copies is < 1 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "打印份数必须在 1–999 之间。");
        }

        WindowsMediaDefinition? media = capabilities.Media.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, request.MediaName, StringComparison.Ordinal)
            || string.Equals(candidate.DriverName, request.MediaName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.DisplayName, request.MediaName, StringComparison.OrdinalIgnoreCase));
        if (media is null && request.CustomMediaSize is { } customSize)
        {
            media = new WindowsMediaDefinition
            {
                Key = request.MediaName,
                DisplayName = $"自定义 {customSize.Width:0.###} × {customSize.Height:0.###} mm",
                DriverName = "Custom",
                Size = customSize,
                PrintableArea = new Rmpp.Domain.Geometry.MmRect(0, 0, customSize.Width, customSize.Height),
                IsCustom = true,
            };
        }
        if (media is null)
        {
            throw new InvalidOperationException($"打印机不支持介质：{request.MediaName}");
        }
        if (!capabilities.Orientations.Contains(request.Orientation))
        {
            throw new InvalidOperationException($"打印机不支持方向：{request.Orientation}");
        }

        WindowsPrintResolution resolution = capabilities.Resolutions.FirstOrDefault(candidate =>
            candidate.DpiX == request.ResolutionDpi && candidate.DpiY == request.ResolutionDpi)
            ?? throw new InvalidOperationException($"打印机不支持 {request.ResolutionDpi} DPI。");
        return new WindowsPrintTicketDefinition
        {
            Media = media,
            Orientation = request.Orientation,
            Resolution = resolution,
            Copies = request.Copies,
        };
    }

    public static PrintTicket ToNative(WindowsPrintTicketDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        PageMediaSize pageMediaSize = Enum.TryParse(definition.Media.DriverName, out PageMediaSizeName mediaName)
            && mediaName != PageMediaSizeName.Unknown
            ? new PageMediaSize(mediaName)
            : new PageMediaSize(
                PrintableAreaService.ToDeviceIndependentPixels(definition.Media.Size.Width),
                PrintableAreaService.ToDeviceIndependentPixels(definition.Media.Size.Height));
        return new PrintTicket
        {
            PageMediaSize = pageMediaSize,
            PageOrientation = definition.Orientation switch
            {
                PrintMediaOrientation.Landscape => PageOrientation.Landscape,
                PrintMediaOrientation.ReversePortrait => PageOrientation.ReversePortrait,
                PrintMediaOrientation.ReverseLandscape => PageOrientation.ReverseLandscape,
                _ => PageOrientation.Portrait,
            },
            PageResolution = new PageResolution(definition.Resolution.DpiX, definition.Resolution.DpiY),
            CopyCount = definition.Copies,
        };
    }
}
