using Rmpp.Domain.Elements;

namespace Rmpp.Rendering.Barcodes;

/// <summary>定义条码校验和模块编码所需的离线参数。</summary>
public sealed record BarcodeOptions
{
    public required BarcodeSymbology Symbology { get; init; }
    public required string Content { get; init; }
    public double QuietZoneMm { get; init; } = 1;
    public int ErrorCorrectionLevel { get; init; }
    public bool ShowHumanReadableText { get; init; } = true;
}
