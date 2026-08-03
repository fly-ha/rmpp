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
    public const double MinimumCenterIconScale = 0.10;
    public const double MaximumCenterIconScale = 0.25;
    public const double DefaultCenterIconScale = 0.20;

    public BarcodeSymbology Symbology { get; init; } = BarcodeSymbology.Code128;
    public ElementExpression Content { get; init; } = ElementExpression.Literal(string.Empty);
    public double QuietZoneMm { get; init; } = 1;
    public int ErrorCorrectionLevel { get; init; }
    public bool ShowHumanReadableText { get; init; } = true;
    public TextStyle HumanReadableTextStyle { get; init; } = new() { FontSizePoints = 8 };
    /// <summary>二维码中心图标使用的包内图片资源；其他条码制式不允许设置。</summary>
    public Guid? CenterIconAssetId { get; init; }
    /// <summary>中心图标相对二维码实际符号短边的比例，限制在可读性安全区间。</summary>
    public double CenterIconScale { get; init; } = DefaultCenterIconScale;

    public override TemplateElement Validate()
    {
        _ = base.Validate();
        if (CenterIconAssetId == Guid.Empty)
        {
            throw new InvalidOperationException("QR center icon asset id cannot be empty.");
        }
        if (!double.IsFinite(CenterIconScale)
            || CenterIconScale < MinimumCenterIconScale
            || CenterIconScale > MaximumCenterIconScale)
        {
            throw new InvalidOperationException(
                $"QR center icon scale must be between {MinimumCenterIconScale:P0} and {MaximumCenterIconScale:P0}.");
        }
        if (CenterIconAssetId.HasValue && Symbology != BarcodeSymbology.QrCode)
        {
            throw new InvalidOperationException("Center icons are supported only by QR codes.");
        }

        return this;
    }
}
