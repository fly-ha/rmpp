using System.Security.Cryptography;
using SkiaSharp;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Fills;
using Rmpp.Rendering.Geometry;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Skia;
using Xunit;

namespace Rmpp.Rendering.Tests.Skia;

public sealed class Phase6GoldenArtifactTests
{
    [Fact]
    public void AllShapesHatchesAndBarcodesProduceDeterministicGallery()
    {
        RenderPage page = BuildGallery();
        SkiaBitmapRenderer renderer = new();
        using SKBitmap first = renderer.Render(page, 150);
        using SKBitmap second = renderer.Render(page, 150);
        byte[] firstPng = Encode(first);
        byte[] secondPng = Encode(second);

        Assert.Equal(SHA256.HashData(firstPng), SHA256.HashData(secondPng));
        string artifactDirectory = FindArtifactDirectory();
        Directory.CreateDirectory(artifactDirectory);
        File.WriteAllBytes(Path.Combine(artifactDirectory, "phase6-gallery.png"), firstPng);

        RenderScene scene = new() { DocumentId = Guid.NewGuid(), Pages = [page] };
        using FileStream pdf = File.Create(Path.Combine(artifactDirectory, "phase6-a4.pdf"));
        new SkiaPdfExporter().Export(scene, pdf);
        Assert.True(firstPng.Length > 10_000);
        Assert.True(pdf.Length > 1_000);
    }

    /// <summary>构造覆盖全部基础形状、八种底纹和九种条码的 A4 验证页。</summary>
    private static RenderPage BuildGallery()
    {
        List<RenderCommand> commands = [];
        ShapeElement[] shapes =
        [
            new LineElement { Bounds = new MmRect(10, 10, 25, 12), Start = new MmPoint(0, 0), End = new MmPoint(25, 12) },
            new RectangleElement { Bounds = new MmRect(45, 10, 25, 12), CornerRadius = new MmSize(3, 3) },
            new EllipseElement { Bounds = new MmRect(80, 10, 25, 12) },
            new ArcElement { Bounds = new MmRect(115, 10, 25, 12), StartAngle = new Angle(20), SweepDegrees = 250 },
            new SectorElement { Bounds = new MmRect(150, 10, 25, 12), StartAngle = new Angle(20), SweepDegrees = 250 },
            new PolylineElement { Bounds = new MmRect(10, 32, 25, 12), Points = [new MmPoint(0, 12), new MmPoint(8, 0), new MmPoint(17, 12), new MmPoint(25, 0)] },
            new PolygonElement { Bounds = new MmRect(45, 32, 25, 12), Points = [new MmPoint(0, 12), new MmPoint(7, 0), new MmPoint(25, 4), new MmPoint(17, 12)] },
        ];
        foreach (ShapeElement shape in shapes)
        {
            commands.Add(new RenderPathCommand
            {
                SourceId = shape.Id,
                Transform = RenderTransform.ForElement(shape.Bounds, shape.Rotation),
                Path = ShapePathFactory.Create(shape),
                Stroke = new RenderStroke { IsEnabled = true, Color = RgbaColor.Black, WidthMm = 0.35 },
                Fill = shape is LineElement or PolylineElement or ArcElement
                    ? new RenderNoFill()
                    : new RenderSolidFill(new RgbaColor(225, 235, 245)),
            });
        }

        HatchPattern[] patterns = Enum.GetValues<HatchPattern>();
        for (int index = 0; index < patterns.Length; index++)
        {
            int column = index % 4;
            int row = index / 4;
            RectangleElement rectangle = new()
            {
                Bounds = new MmRect(10 + column * 45, 58 + row * 22, 35, 15),
            };
            commands.Add(new RenderPathCommand
            {
                SourceId = rectangle.Id,
                Transform = RenderTransform.ForElement(rectangle.Bounds, Angle.Zero),
                Path = ShapePathFactory.Create(rectangle),
                Stroke = new RenderStroke { IsEnabled = true, Color = RgbaColor.Black, WidthMm = 0.2 },
                Fill = HatchPatternFactory.Create(new HatchFill
                {
                    Pattern = patterns[index],
                    SpacingMm = 2.5,
                    LineWidthMm = 0.18,
                    Foreground = new RgbaColor(20, 70, 130),
                    Background = RgbaColor.White,
                }),
            });
        }

        (BarcodeSymbology Symbology, string Content)[] barcodes =
        [
            (BarcodeSymbology.Code128, "RMPP-128"),
            (BarcodeSymbology.Code39, "RMPP-39"),
            (BarcodeSymbology.Ean13, "5901234123457"),
            (BarcodeSymbology.Ean8, "96385074"),
            (BarcodeSymbology.UpcA, "036000291452"),
            (BarcodeSymbology.Interleaved2Of5, "123456"),
            (BarcodeSymbology.Codabar, "A1234B"),
            (BarcodeSymbology.QrCode, "RMPP QR"),
            (BarcodeSymbology.DataMatrix, "RMPP-DM"),
        ];
        for (int index = 0; index < barcodes.Length; index++)
        {
            int column = index % 3;
            int row = index / 3;
            MmRect bounds = new(10 + column * 65, 112 + row * 48, 55, 38);
            commands.Add(new RenderBarcodeCommand
            {
                SourceId = Guid.NewGuid(),
                Transform = RenderTransform.Translation(bounds.X, bounds.Y),
                LocalBounds = new MmRect(0, 0, bounds.Width, bounds.Height),
                Content = barcodes[index].Content,
                Symbology = barcodes[index].Symbology,
                QuietZoneMm = 1.5,
                ShowHumanReadableText = false,
                HumanReadableTextStyle = new RenderTextStyle
                {
                    FontFamily = "Microsoft YaHei",
                    FontSizePoints = 7,
                    Color = RgbaColor.Black,
                    LineSpacing = 1,
                },
            });
        }

        MmSize pageSize = new(210, 297);
        return new RenderPage
        {
            PageNumber = 1,
            Size = pageSize,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, pageSize.Width, pageSize.Height)),
            Commands = commands,
        };
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string FindArtifactDirectory()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Rmpp.sln")))
        {
            current = current.Parent;
        }

        string root = current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        return Path.Combine(root, "TestResults", "phase6");
    }
}
