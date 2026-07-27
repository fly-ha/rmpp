using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using Rmpp.Domain.Elements;

namespace Rmpp.Rendering.Barcodes;

/// <summary>把已验证内容编码为无分辨率含义的黑白模块矩阵。</summary>
public sealed class BarcodeRenderService(BarcodeValidator? validator = null)
{
    private readonly BarcodeValidator validator = validator ?? new BarcodeValidator();

    public BarcodeMatrix Encode(BarcodeOptions options)
    {
        BarcodeValidationResult validation = validator.Validate(options);
        if (!validation.IsValid)
        {
            throw new ArgumentException(string.Join(" ", validation.Issues.Select(static issue => issue.Message)), nameof(options));
        }

        Dictionary<EncodeHintType, object> hints = new()
        {
            [EncodeHintType.MARGIN] = 0,
            [EncodeHintType.CHARACTER_SET] = Encoding.UTF8.WebName,
        };
        if (options.Symbology == BarcodeSymbology.QrCode)
        {
            hints[EncodeHintType.ERROR_CORRECTION] = options.ErrorCorrectionLevel switch
            {
                1 => ErrorCorrectionLevel.M,
                2 => ErrorCorrectionLevel.Q,
                3 => ErrorCorrectionLevel.H,
                _ => ErrorCorrectionLevel.L,
            };
        }

        BitMatrix matrix = new MultiFormatWriter().encode(
            options.Content,
            Map(options.Symbology),
            1,
            1,
            hints);
        bool[] modules = new bool[checked(matrix.Width * matrix.Height)];
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                modules[y * matrix.Width + x] = matrix[x, y];
            }
        }

        return new BarcodeMatrix(matrix.Width, matrix.Height, modules);
    }

    private static BarcodeFormat Map(BarcodeSymbology symbology) => symbology switch
    {
        BarcodeSymbology.Code128 => BarcodeFormat.CODE_128,
        BarcodeSymbology.Code39 => BarcodeFormat.CODE_39,
        BarcodeSymbology.Ean13 => BarcodeFormat.EAN_13,
        BarcodeSymbology.Ean8 => BarcodeFormat.EAN_8,
        BarcodeSymbology.UpcA => BarcodeFormat.UPC_A,
        BarcodeSymbology.Interleaved2Of5 => BarcodeFormat.ITF,
        BarcodeSymbology.Codabar => BarcodeFormat.CODABAR,
        BarcodeSymbology.QrCode => BarcodeFormat.QR_CODE,
        BarcodeSymbology.DataMatrix => BarcodeFormat.DATA_MATRIX,
        _ => throw new ArgumentOutOfRangeException(nameof(symbology)),
    };
}

public sealed record BarcodeMatrix(int Width, int Height, IReadOnlyList<bool> Modules)
{
    public bool this[int x, int y] => Modules[y * Width + x];
}
