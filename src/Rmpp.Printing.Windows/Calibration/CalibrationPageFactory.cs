using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Scene;

namespace Rmpp.Printing.Windows.Calibration;

/// <summary>生成包含毫米尺、100mm参考框、中心十字和进纸方向的离线校准页。</summary>
public sealed class CalibrationPageFactory
{
    private static readonly Guid CalibrationSourceId = new("4cf0e72d-2a15-4fa2-8879-a2911ef48318");

    public static RenderScene Create(MmSize mediaSize)
    {
        if (mediaSize.Width < 80 || mediaSize.Height < 80)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaSize), "校准介质至少需要 80mm × 80mm。");
        }

        List<RenderCommand> commands = [];
        AddBorderAndReferenceBox(commands, mediaSize);
        AddRulers(commands, mediaSize);
        AddDirectionMark(commands, mediaSize);
        commands.Add(CreateInstructions(mediaSize));

        RenderPage page = new()
        {
            PageNumber = 1,
            Size = mediaSize,
            Clip = RenderClip.FromRectangle(new MmRect(0, 0, mediaSize.Width, mediaSize.Height)),
            Commands = commands,
        };
        return new RenderScene { DocumentId = CalibrationSourceId, Pages = [page] };
    }

    private static void AddBorderAndReferenceBox(List<RenderCommand> commands, MmSize mediaSize)
    {
        commands.Add(Path(Rectangle(5, 5, mediaSize.Width - 10, mediaSize.Height - 10), 0.3));
        double referenceWidth = Math.Min(100, mediaSize.Width - 30);
        double referenceHeight = Math.Min(100, mediaSize.Height - 50);
        commands.Add(Path(Rectangle(15, 25, referenceWidth, referenceHeight), 0.25));
        double centerX = mediaSize.Width / 2;
        double centerY = mediaSize.Height / 2;
        commands.Add(Path(new RenderPathBuilder()
            .MoveTo(new MmPoint(centerX - 10, centerY)).LineTo(new MmPoint(centerX + 10, centerY))
            .MoveTo(new MmPoint(centerX, centerY - 10)).LineTo(new MmPoint(centerX, centerY + 10))
            .Build(), 0.2));
    }

    private static void AddRulers(List<RenderCommand> commands, MmSize mediaSize)
    {
        RenderPathBuilder ruler = new();
        for (int x = 10; x <= Math.Floor(mediaSize.Width - 10); x += 5)
        {
            double length = x % 10 == 0 ? 5 : 2.5;
            ruler.MoveTo(new MmPoint(x, 10)).LineTo(new MmPoint(x, 10 + length));
        }

        for (int y = 10; y <= Math.Floor(mediaSize.Height - 10); y += 5)
        {
            double length = y % 10 == 0 ? 5 : 2.5;
            ruler.MoveTo(new MmPoint(10, y)).LineTo(new MmPoint(10 + length, y));
        }

        commands.Add(Path(ruler.Build(), 0.15));
    }

    private static void AddDirectionMark(List<RenderCommand> commands, MmSize mediaSize)
    {
        double centerX = mediaSize.Width / 2;
        commands.Add(Path(new RenderPathBuilder()
            .MoveTo(new MmPoint(centerX, 7)).LineTo(new MmPoint(centerX, 20))
            .MoveTo(new MmPoint(centerX, 7)).LineTo(new MmPoint(centerX - 3, 12))
            .MoveTo(new MmPoint(centerX, 7)).LineTo(new MmPoint(centerX + 3, 12))
            .Build(), 0.4));
    }

    private static RenderTextCommand CreateInstructions(MmSize mediaSize) => new()
    {
        SourceId = CalibrationSourceId,
        ZIndex = 10,
        Text = "RMPP 校准页：请关闭驱动适页缩放，以毫米尺测量参考框、原点偏移和旋转角度。箭头指向进纸前端。",
        LocalBounds = new MmRect(15, mediaSize.Height - 20, mediaSize.Width - 30, 12),
        Style = new RenderTextStyle
        {
            FontFamily = "Microsoft YaHei",
            FontSizePoints = 8,
            Color = RgbaColor.Black,
            Wrap = true,
            LineSpacing = 1,
            OverflowMode = TextOverflowMode.Warn,
        },
    };

    private static RenderPathCommand Path(RenderPath path, double widthMm) => new()
    {
        SourceId = CalibrationSourceId,
        Path = path,
        Stroke = new RenderStroke { IsEnabled = true, Color = RgbaColor.Black, WidthMm = widthMm },
        Fill = new RenderNoFill(),
    };

    private static RenderPath Rectangle(double x, double y, double width, double height) =>
        new RenderPathBuilder()
            .MoveTo(new MmPoint(x, y))
            .LineTo(new MmPoint(x + width, y))
            .LineTo(new MmPoint(x + width, y + height))
            .LineTo(new MmPoint(x, y + height))
            .Close()
            .Build();
}
