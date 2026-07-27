using Rmpp.Domain.Geometry;

namespace Rmpp.Printing.Windows.Printers;

/// <summary>驱动介质的稳定归一化定义；DriverName 只在 Windows 边界创建 PrintTicket 时使用。</summary>
public sealed record WindowsMediaDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string DriverName { get; init; }
    public required MmSize Size { get; init; }
    public MmRect? PrintableArea { get; init; }
    public bool IsCustom { get; init; }
}

/// <summary>驱动报告的对称或非对称打印分辨率。</summary>
public sealed record WindowsPrintResolution(int DpiX, int DpiY)
{
    public string Key => $"{DpiX}x{DpiY}";
}
