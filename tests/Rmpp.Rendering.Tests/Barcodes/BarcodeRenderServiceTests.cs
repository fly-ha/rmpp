using ZXing;
using ZXing.Common;
using Rmpp.Domain.Elements;
using Rmpp.Rendering.Barcodes;
using Xunit;

namespace Rmpp.Rendering.Tests.Barcodes;

public sealed class BarcodeRenderServiceTests
{
    public static TheoryData<BarcodeSymbology, string> ReadableSamples => new()
    {
        { BarcodeSymbology.Code128, "RMPP-128" },
        { BarcodeSymbology.Code39, "RMPP-39" },
        { BarcodeSymbology.Ean13, "5901234123457" },
        { BarcodeSymbology.Ean8, "96385074" },
        { BarcodeSymbology.UpcA, "036000291452" },
        { BarcodeSymbology.Interleaved2Of5, "123456" },
        { BarcodeSymbology.Codabar, "A1234B" },
        { BarcodeSymbology.QrCode, "RMPP QR" },
        { BarcodeSymbology.DataMatrix, "RMPP-DM" },
    };

    [Theory]
    [MemberData(nameof(ReadableSamples))]
    public void EncodedModulesCanBeReadBackOffline(BarcodeSymbology symbology, string content)
    {
        BarcodeMatrix matrix = new BarcodeRenderService().Encode(new BarcodeOptions
        {
            Symbology = symbology,
            Content = content,
        });
        (byte[] pixels, int width, int height) = Expand(matrix, symbology);
        BinaryBitmap bitmap = new(new HybridBinarizer(new RGBLuminanceSource(
            pixels,
            width,
            height,
            RGBLuminanceSource.BitmapFormat.Gray8)));
        Result? decoded = new MultiFormatReader().decode(bitmap, new Dictionary<DecodeHintType, object>
        {
            [DecodeHintType.TRY_HARDER] = true,
            [DecodeHintType.POSSIBLE_FORMATS] = new[] { Map(symbology) },
        });

        Assert.NotNull(decoded);
        if (symbology != BarcodeSymbology.Codabar)
        {
            Assert.Equal(content, decoded.Text);
        }
        else
        {
            Assert.Contains("1234", decoded.Text, StringComparison.Ordinal);
        }
    }

    private static (byte[] Pixels, int Width, int Height) Expand(BarcodeMatrix matrix, BarcodeSymbology symbology)
    {
        bool twoDimensional = symbology is BarcodeSymbology.QrCode or BarcodeSymbology.DataMatrix;
        int scale = twoDimensional ? 8 : 4;
        int quiet = twoDimensional ? 8 : 12;
        int width = checked((matrix.Width + quiet * 2) * scale);
        int height = twoDimensional
            ? checked((matrix.Height + quiet * 2) * scale)
            : 160;
        byte[] pixels = Enumerable.Repeat(byte.MaxValue, checked(width * height)).ToArray();
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                if (!matrix[x, y])
                {
                    continue;
                }

                int startX = (x + quiet) * scale;
                int startY = twoDimensional ? (y + quiet) * scale : quiet;
                int moduleHeight = twoDimensional ? scale : height - quiet * 2;
                for (int py = startY; py < startY + moduleHeight; py++)
                {
                    Array.Fill(pixels, (byte)0, py * width + startX, scale);
                }
            }
        }

        return (pixels, width, height);
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
