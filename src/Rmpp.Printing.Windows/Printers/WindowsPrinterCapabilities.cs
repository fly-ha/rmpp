using Rmpp.Application.Abstractions;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;

namespace Rmpp.Printing.Windows.Printers;

/// <summary>不携带 System.Printing 对象的打印机能力快照，可安全传给 ViewModel 和测试。</summary>
public sealed record WindowsPrinterCapabilities
{
    public required PrinterIdentity Identity { get; init; }
    public required string QueueName { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyList<WindowsMediaDefinition> Media { get; init; } = Array.Empty<WindowsMediaDefinition>();
    public IReadOnlySet<PrintMediaOrientation> Orientations { get; init; } = new HashSet<PrintMediaOrientation>();
    public IReadOnlyList<WindowsPrintResolution> Resolutions { get; init; } = Array.Empty<WindowsPrintResolution>();
    public MmRect? DefaultPrintableArea { get; init; }
    public bool SupportsDuplex { get; init; }
}

/// <summary>从 Windows 打印子系统读取的原始但已脱离驱动对象生命周期的队列快照。</summary>
public sealed record WindowsPrintQueueSnapshot
{
    public required string ServerName { get; init; }
    public required string QueueName { get; init; }
    public required string DisplayName { get; init; }
    public required string DriverName { get; init; }
    public required string PortName { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyList<WindowsMediaDefinition> Media { get; init; } = Array.Empty<WindowsMediaDefinition>();
    public IReadOnlySet<PrintMediaOrientation> Orientations { get; init; } = new HashSet<PrintMediaOrientation>();
    public IReadOnlyList<WindowsPrintResolution> Resolutions { get; init; } = Array.Empty<WindowsPrintResolution>();
    public MmRect? DefaultPrintableArea { get; init; }
    public bool SupportsDuplex { get; init; }
}
