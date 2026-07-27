using SkiaSharp;
using ZXing;
using ZXing.Common;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Xunit;

namespace Rmpp.Rendering.Tests.Skia;

public sealed class SkiaBarcodeReadbackTests
{
    public static TheoryData<BarcodeSymbology, string> Samples => new()
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
    [MemberData(nameof(Samples))]
    public void SkiaRenderedBarcodeCanBeReadBack(BarcodeSymbology symbology, string content)
    {
        MmSize pageSize = new(70, 35);
        RenderBarcodeCommand command = new()
        {
            SourceId = Guid.NewGuid(),
            Transform = RenderTransform.Translation(5, 5),
            LocalBounds = new MmRect(0, 0, 60, 25),
            Content = content,
            Symbology = symbology,
            QuietZoneMm = 3,
            ShowHumanReadableText = false,
            HumanReadableTextStyle = new RenderTextStyle
            {
                FontFamily = "Microsoft YaHei",
                FontSizePoints = 8,
                Color = RgbaColor.Black,
                LineSpacing = 1,
            },
        };
        RenderPage page = new()
        {
            PageNumber = 1,
            Size = pageSize,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, pageSize.Width, pageSize.Height)),
            Commands = [command],
        };
        using SKBitmap rendered = new SkiaBitmapRenderer().Render(page, 300);
        byte[] gray = new byte[checked(rendered.Width * rendered.Height)];
        for (int y = 0; y < rendered.Height; y++)
        {
            for (int x = 0; x < rendered.Width; x++)
            {
                SKColor color = rendered.GetPixel(x, y);
                gray[y * rendered.Width + x] = checked((byte)((color.Red + color.Green + color.Blue) / 3));
            }
        }

        BinaryBitmap bitmap = new(new HybridBinarizer(new RGBLuminanceSource(
            gray,
            rendered.Width,
            rendered.Height,
            RGBLuminanceSource.BitmapFormat.Gray8)));
        Result? decoded = new MultiFormatReader().decode(bitmap, new Dictionary<DecodeHintType, object>
        {
            [DecodeHintType.TRY_HARDER] = true,
            [DecodeHintType.PURE_BARCODE] = true,
            [DecodeHintType.POSSIBLE_FORMATS] = new[] { Map(symbology) },
        });

        Assert.NotNull(decoded);
        if (symbology != BarcodeSymbology.Codabar)
        {
            Assert.Equal(content, decoded.Text);
        }
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
