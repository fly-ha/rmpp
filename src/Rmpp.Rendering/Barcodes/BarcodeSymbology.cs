using Rmpp.Domain.Elements;

namespace Rmpp.Rendering.Barcodes;

/// <summary>首版经验证并固定公开的九种条码制式。</summary>
public static class SupportedBarcodeSymbologies
{
    public static IReadOnlyList<BarcodeSymbology> Version1 { get; } =
    [
        BarcodeSymbology.Code128,
        BarcodeSymbology.Code39,
        BarcodeSymbology.Ean13,
        BarcodeSymbology.Ean8,
        BarcodeSymbology.UpcA,
        BarcodeSymbology.Interleaved2Of5,
        BarcodeSymbology.Codabar,
        BarcodeSymbology.QrCode,
        BarcodeSymbology.DataMatrix,
    ];
}
