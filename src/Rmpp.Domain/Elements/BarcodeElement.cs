using Rmpp.Domain.Data;
using Rmpp.Domain.Styles;

namespace Rmpp.Domain.Elements;

public enum BarcodeSymbology
{
    Code128,
    Code39,
    Ean13,
    Ean8,
    UpcA,
    Interleaved2Of5,
    Codabar,
    QrCode,
    DataMatrix
}

public sealed record BarcodeElement : TemplateElement
{
    public BarcodeSymbology Symbology { get; init; } = BarcodeSymbology.Code128;
    public ElementExpression Content { get; init; } = ElementExpression.Literal(string.Empty);
    public double QuietZoneMm { get; init; } = 1;
    public int ErrorCorrectionLevel { get; init; }
    public bool ShowHumanReadableText { get; init; } = true;
    public TextStyle HumanReadableTextStyle { get; init; } = new() { FontSizePoints = 8 };
}
