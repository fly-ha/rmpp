using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Printing.Windows.Printers;
using Rmpp.Rendering.Barcodes;
using Rmpp.Rendering.Scene;
using DomainTransform = Rmpp.Rendering.Scene.RenderTransform;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfGeometry = System.Windows.Media.Geometry;
using WpfPen = System.Windows.Media.Pen;

namespace Rmpp.Printing.Windows.Rendering;

/// <summary>将共享毫米制场景转成 WPF/XPS DrawingVisual；不对页面执行适页缩放。</summary>
public sealed class WpfPrintSceneRenderer(
    IPrintImageResolver? imageResolver = null,
    BarcodeRenderService? barcodeRenderer = null)
{
    private const double MillimetresPerPoint = 25.4 / 72;
    private readonly IPrintImageResolver imageResolver = imageResolver ?? new LocalPrintImageResolver();
    private readonly BarcodeRenderService barcodeRenderer = barcodeRenderer ?? new();

    public WpfRenderedPage Render(RenderPage page, DomainTransform? finalPhysicalTransform = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        DomainTransform finalTransform = finalPhysicalTransform ?? DomainTransform.Identity;
        DrawingVisual visual = new();
        using DrawingContext context = visual.RenderOpen();
        Size pageSizeDip = ToDip(page.Size);
        context.DrawRectangle(Brushes.White, null, new Rect(new Point(0, 0), pageSizeDip));
        context.PushClip(new RectangleGeometry(new Rect(new Point(0, 0), pageSizeDip)));

        foreach (RenderCommand command in page.Commands.OrderBy(static command => command.ZIndex))
        {
            DrawCommand(context, command, finalTransform);
        }

        context.Pop();
        return new WpfRenderedPage(visual, pageSizeDip);
    }

    private void DrawCommand(DrawingContext context, RenderCommand command, DomainTransform finalTransform)
    {
        int pushed = 0;
        if (command.Opacity < 1)
        {
            context.PushOpacity(Math.Clamp(command.Opacity, 0, 1));
            pushed++;
        }

        context.PushTransform(ToWpfTransform(command.Transform.Then(finalTransform)));
        pushed++;
        if (command.Clip is not null)
        {
            context.PushClip(ToGeometry(command.Clip));
            pushed++;
        }

        switch (command)
        {
            case RenderPathCommand path:
                context.DrawGeometry(ToBrush(path.Fill), ToPen(path.Stroke), ToGeometry(path.Path));
                break;
            case RenderTextCommand text:
                DrawText(context, text);
                break;
            case RenderImageCommand image:
                DrawImage(context, image);
                break;
            case RenderBarcodeCommand barcode:
                DrawBarcode(context, barcode);
                break;
            default:
                throw new NotSupportedException($"不支持的打印命令：{command.GetType().Name}");
        }

        while (pushed-- > 0)
        {
            context.Pop();
        }
    }

    private static void DrawText(DrawingContext context, RenderTextCommand command)
    {
        if (command.GlyphRuns.Count > 0 && DrawGlyphRuns(context, command.GlyphRuns))
        {
            return;
        }

        RenderTextStyle style = command.Style;
        Typeface typeface = new(
            new FontFamily(style.FontFamily),
            style.IsItalic ? FontStyles.Italic : FontStyles.Normal,
            style.IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);
        FormattedText text = new(
            command.Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            style.FontSizePoints * MillimetresPerPoint,
            ToBrush(style.Color),
            1);
        text.MaxTextWidth = Math.Max(0.01, command.LocalBounds.Width);
        text.MaxTextHeight = Math.Max(0.01, command.LocalBounds.Height);
        text.TextAlignment = style.HorizontalAlignment switch
        {
            TextHorizontalAlignment.Center => TextAlignment.Center,
            TextHorizontalAlignment.Right => TextAlignment.Right,
            TextHorizontalAlignment.Justify => TextAlignment.Justify,
            _ => TextAlignment.Left,
        };
        if (style.IsUnderline)
        {
            text.SetTextDecorations(TextDecorations.Underline);
        }

        double y = style.VerticalAlignment switch
        {
            TextVerticalAlignment.Center => command.LocalBounds.Y + Math.Max(0, (command.LocalBounds.Height - text.Height) / 2),
            TextVerticalAlignment.Bottom => command.LocalBounds.Bottom - text.Height,
            _ => command.LocalBounds.Y,
        };
        context.DrawText(text, new Point(command.LocalBounds.X, y));
    }

    private static bool DrawGlyphRuns(DrawingContext context, IReadOnlyList<RenderGlyphRun> runs)
    {
        foreach (RenderGlyphRun run in runs)
        {
            Typeface typeface = new(run.FontFace);
            if (!typeface.TryGetGlyphTypeface(out GlyphTypeface? glyphTypeface)
                || run.Glyphs.Any(glyph => glyph.GlyphId < 0 || glyph.GlyphId >= glyphTypeface.GlyphCount))
            {
                return false;
            }
        }

        foreach (RenderGlyphRun run in runs)
        {
            _ = new Typeface(run.FontFace).TryGetGlyphTypeface(out GlyphTypeface? glyphTypeface);
            foreach (RenderGlyph glyph in run.Glyphs)
            {
                GlyphRun glyphRun = new(
                    glyphTypeface!,
                    0,
                    false,
                    run.FontSizePoints * MillimetresPerPoint,
                    1,
                    [(ushort)glyph.GlyphId],
                    new Point(glyph.Origin.X, glyph.Origin.Y),
                    [glyph.AdvanceMm],
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
                context.DrawGlyphRun(ToBrush(run.Color), glyphRun);
            }
        }

        return true;
    }

    private void DrawImage(DrawingContext context, RenderImageCommand command)
    {
        WpfBrush? fill = ToBrush(command.Fill);
        if (fill is not null)
        {
            context.DrawRectangle(fill, null, ToRect(command.LocalBounds));
        }

        BitmapSource? bitmap = imageResolver.Resolve(command.Image);
        if (bitmap is not null)
        {
            Rect destination = CalculateImageDestination(command.LocalBounds, bitmap, command.Image.FitMode);
            context.DrawImage(bitmap, destination);
        }
        else
        {
            WpfPen placeholder = new(Brushes.Gray, 0.2);
            Rect bounds = ToRect(command.LocalBounds);
            context.DrawRectangle(null, placeholder, bounds);
            context.DrawLine(placeholder, bounds.TopLeft, bounds.BottomRight);
            context.DrawLine(placeholder, bounds.BottomLeft, bounds.TopRight);
        }

        WpfPen? border = ToPen(command.Border);
        if (border is not null)
        {
            context.DrawRectangle(null, border, ToRect(command.LocalBounds));
        }
    }

    private void DrawBarcode(DrawingContext context, RenderBarcodeCommand command)
    {
        BarcodeMatrix matrix = barcodeRenderer.Encode(new BarcodeOptions
        {
            Symbology = command.Symbology,
            Content = command.Content,
            QuietZoneMm = command.QuietZoneMm,
            ErrorCorrectionLevel = command.ErrorCorrectionLevel,
            ShowHumanReadableText = command.ShowHumanReadableText,
        });
        double availableWidth = Math.Max(0, command.LocalBounds.Width - command.QuietZoneMm * 2);
        double availableHeight = Math.Max(0, command.LocalBounds.Height - command.QuietZoneMm * 2);
        double module = Math.Min(availableWidth / matrix.Width, availableHeight / matrix.Height);
        if (module <= 0)
        {
            return;
        }

        double contentWidth = module * matrix.Width;
        double contentHeight = module * matrix.Height;
        double originX = command.LocalBounds.X + (command.LocalBounds.Width - contentWidth) / 2;
        double originY = command.LocalBounds.Y + (command.LocalBounds.Height - contentHeight) / 2;
        for (int y = 0; y < matrix.Height; y++)
        {
            int runStart = -1;
            for (int x = 0; x <= matrix.Width; x++)
            {
                bool dark = x < matrix.Width && matrix[x, y];
                if (dark && runStart < 0)
                {
                    runStart = x;
                }
                else if (!dark && runStart >= 0)
                {
                    context.DrawRectangle(
                        Brushes.Black,
                        null,
                        new Rect(originX + runStart * module, originY + y * module, (x - runStart) * module, module));
                    runStart = -1;
                }
            }
        }
    }

    private static Rect CalculateImageDestination(MmRect bounds, BitmapSource bitmap, ImageFitMode fitMode)
    {
        if (fitMode == ImageFitMode.Stretch || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return ToRect(bounds);
        }

        double sourceRatio = (double)bitmap.PixelWidth / bitmap.PixelHeight;
        double targetRatio = bounds.Width / Math.Max(bounds.Height, double.Epsilon);
        bool fitWidth = fitMode == ImageFitMode.Contain ? sourceRatio >= targetRatio : sourceRatio < targetRatio;
        double width = fitWidth ? bounds.Width : bounds.Height * sourceRatio;
        double height = fitWidth ? bounds.Width / sourceRatio : bounds.Height;
        return new Rect(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }

    private static WpfGeometry ToGeometry(RenderClip clip) => clip.Path is not null
        ? ToGeometry(clip.Path)
        : clip.Rectangle is { } rectangle
            ? new RectangleGeometry(ToRect(rectangle))
            : WpfGeometry.Empty;

    private static PathGeometry ToGeometry(RenderPath path)
    {
        PathGeometry geometry = new() { FillRule = path.FillRule == RenderFillRule.EvenOdd ? FillRule.EvenOdd : FillRule.Nonzero };
        PathFigure? figure = null;
        foreach (RenderPathSegment segment in path.Segments)
        {
            switch (segment)
            {
                case RenderMoveTo move:
                    figure = new PathFigure { StartPoint = ToPoint(move.Point) };
                    geometry.Figures.Add(figure);
                    break;
                case RenderLineTo line when figure is not null:
                    figure.Segments.Add(new LineSegment(ToPoint(line.Point), true));
                    break;
                case RenderCubicTo cubic when figure is not null:
                    figure.Segments.Add(new BezierSegment(
                        ToPoint(cubic.Control1),
                        ToPoint(cubic.Control2),
                        ToPoint(cubic.End),
                        true));
                    break;
                case RenderClosePath when figure is not null:
                    figure.IsClosed = true;
                    break;
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static WpfBrush? ToBrush(RenderFill fill) => fill switch
    {
        RenderNoFill => null,
        RenderSolidFill solid => ToBrush(solid.Color),
        RenderHatchFill hatch => CreateHatchBrush(hatch),
        _ => throw new NotSupportedException($"不支持的填充：{fill.GetType().Name}"),
    };

    private static DrawingBrush CreateHatchBrush(RenderHatchFill hatch)
    {
        DrawingGroup drawing = new();
        drawing.Children.Add(new GeometryDrawing(
            ToBrush(hatch.Background),
            null,
            new RectangleGeometry(new Rect(0, 0, hatch.Tile.Size.Width, hatch.Tile.Size.Height))));
        WpfPen pen = new(ToBrush(hatch.Foreground), hatch.Tile.LineWidthMm);
        foreach (HatchPrimitive primitive in hatch.Tile.Primitives)
        {
            switch (primitive)
            {
                case HatchLine line:
                    drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(ToPoint(line.Start), ToPoint(line.End))));
                    break;
                case HatchDot dot:
                    drawing.Children.Add(new GeometryDrawing(ToBrush(hatch.Foreground), null, new EllipseGeometry(ToPoint(dot.Center), dot.RadiusMm, dot.RadiusMm)));
                    break;
            }
        }

        DrawingBrush brush = new(drawing)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, hatch.Tile.Size.Width, hatch.Tile.Size.Height),
            Viewbox = new Rect(0, 0, hatch.Tile.Size.Width, hatch.Tile.Size.Height),
            Transform = new RotateTransform(hatch.Tile.RotationDegrees),
        };
        brush.Freeze();
        return brush;
    }

    private static WpfPen? ToPen(RenderStroke stroke)
    {
        if (!stroke.IsEnabled || stroke.WidthMm <= 0)
        {
            return null;
        }

        WpfPen pen = new(ToBrush(stroke.Color), stroke.WidthMm)
        {
            StartLineCap = MapLineCap(stroke.LineCap),
            EndLineCap = MapLineCap(stroke.LineCap),
            LineJoin = stroke.LineJoin switch
            {
                StrokeLineJoin.Bevel => PenLineJoin.Bevel,
                StrokeLineJoin.Round => PenLineJoin.Round,
                _ => PenLineJoin.Miter,
            },
        };
        if (stroke.DashSegmentsMm.Count > 0)
        {
            pen.DashStyle = new DashStyle(stroke.DashSegmentsMm.Select(value => value / stroke.WidthMm), 0);
        }

        pen.Freeze();
        return pen;
    }

    private static PenLineCap MapLineCap(StrokeLineCap cap) => cap switch
    {
        StrokeLineCap.Round => PenLineCap.Round,
        StrokeLineCap.Square => PenLineCap.Square,
        _ => PenLineCap.Flat,
    };

    private static SolidColorBrush ToBrush(RgbaColor color)
    {
        SolidColorBrush brush = new(WpfColor.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        brush.Freeze();
        return brush;
    }

    private static MatrixTransform ToWpfTransform(DomainTransform transform)
    {
        double dipPerMm = PrintableAreaService.DipPerInch / PrintableAreaService.MillimetresPerInch;
        Matrix matrix = new(
            transform.M11 * dipPerMm,
            transform.M12 * dipPerMm,
            transform.M21 * dipPerMm,
            transform.M22 * dipPerMm,
            transform.OffsetX * dipPerMm,
            transform.OffsetY * dipPerMm);
        MatrixTransform result = new(matrix);
        result.Freeze();
        return result;
    }

    private static Point ToPoint(MmPoint point) => new(point.X, point.Y);
    private static Rect ToRect(MmRect rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    private static Size ToDip(MmSize size) => new(
        PrintableAreaService.ToDeviceIndependentPixels(size.Width),
        PrintableAreaService.ToDeviceIndependentPixels(size.Height));
}

/// <summary>一张可交给 DocumentPaginator/XPS writer 的 WPF 页面。</summary>
public sealed record WpfRenderedPage(DrawingVisual Visual, Size Size);
