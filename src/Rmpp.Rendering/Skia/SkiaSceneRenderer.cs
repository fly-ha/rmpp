using SkiaSharp;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Rendering.Barcodes;
using Rmpp.Rendering.Images;
using Rmpp.Rendering.Scene;
using Rmpp.Rendering.Text;

namespace Rmpp.Rendering.Skia;

/// <summary>把后端无关场景绘制到 Skia 画布；画布缩放由调用者显式提供。</summary>
public sealed class SkiaSceneRenderer(
    BarcodeRenderService? barcodeService = null,
    LocalFontCatalog? fontCatalog = null,
    TextLayoutService? textLayoutService = null)
{
    private const double PointsToMillimetres = 25.4 / 72;
    private readonly BarcodeRenderService barcodeService = barcodeService ?? new BarcodeRenderService();
    private readonly LocalFontCatalog fontCatalog = fontCatalog ?? new LocalFontCatalog();
    private readonly TextLayoutService textLayoutService = textLayoutService ?? new TextLayoutService();

    public void RenderPage(
        SKCanvas canvas,
        RenderPage page,
        double unitsPerMillimetre,
        IRenderAssetProvider? assetProvider = null,
        SkiaRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(page);
        if (!double.IsFinite(unitsPerMillimetre) || unitsPerMillimetre <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerMillimetre));
        }

        SkiaRenderOptions effectiveOptions = options ?? new SkiaRenderOptions();
        canvas.Save();
        try
        {
            canvas.Scale(checked((float)unitsPerMillimetre));
            canvas.ClipRect(ToRect(page.Clip.Rectangle ?? new MmRect(0, 0, page.Size.Width, page.Size.Height)));
            foreach (RenderCommand command in page.Commands)
            {
                RenderCommand(canvas, command, unitsPerMillimetre, assetProvider, effectiveOptions);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void RenderCommand(
        SKCanvas canvas,
        RenderCommand command,
        double unitsPerMillimetre,
        IRenderAssetProvider? assetProvider,
        SkiaRenderOptions options)
    {
        canvas.Save();
        try
        {
            SKMatrix matrix = ToMatrix(command.Transform);
            canvas.Concat(matrix);
            ApplyClip(canvas, command.Clip);
            switch (command)
            {
                case RenderPathCommand path:
                    DrawPath(canvas, path, command.Opacity);
                    break;
                case RenderTextCommand text:
                    DrawText(canvas, text, command.Opacity);
                    break;
                case RenderImageCommand image:
                    DrawImage(canvas, image, command.Opacity, assetProvider, options);
                    break;
                case RenderBarcodeCommand barcode:
                    DrawBarcode(canvas, barcode, command.Opacity, unitsPerMillimetre, assetProvider, options);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported render command: {command.GetType().Name}");
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static void ApplyClip(SKCanvas canvas, RenderClip? clip)
    {
        if (clip?.Rectangle is { } rectangle)
        {
            canvas.ClipRect(ToRect(rectangle));
        }

        if (clip?.Path is { } path)
        {
            using SKPath skPath = ToPath(path);
            canvas.ClipPath(skPath, antialias: true);
        }
    }

    private static void DrawPath(SKCanvas canvas, RenderPathCommand command, double opacity)
    {
        using SKPath path = ToPath(command.Path);
        DrawFill(canvas, path, command.Fill, opacity);
        DrawStroke(canvas, path, command.Stroke, opacity);
    }

    private static void DrawFill(SKCanvas canvas, SKPath path, RenderFill fill, double opacity)
    {
        switch (fill)
        {
            case RenderNoFill:
                return;
            case RenderSolidFill solid:
                using (SKPaint paint = CreatePaint(solid.Color, opacity, SKPaintStyle.Fill))
                {
                    canvas.DrawPath(path, paint);
                }
                return;
            case RenderHatchFill hatch:
                DrawHatch(canvas, path, hatch, opacity);
                return;
            default:
                throw new NotSupportedException($"Unsupported fill: {fill.GetType().Name}");
        }
    }

    /// <summary>在路径裁剪内重复绘制毫米制底纹单元，PDF 和位图共享相同几何。</summary>
    private static void DrawHatch(SKCanvas canvas, SKPath path, RenderHatchFill hatch, double opacity)
    {
        using (SKPaint background = CreatePaint(hatch.Background, opacity, SKPaintStyle.Fill))
        {
            canvas.DrawPath(path, background);
        }

        SKRect bounds = path.Bounds;
        double tileWidth = hatch.Tile.Size.Width;
        double tileHeight = hatch.Tile.Size.Height;
        double padding = Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) + Math.Max(tileWidth, tileHeight);
        canvas.Save();
        try
        {
            canvas.ClipPath(path, antialias: true);
            canvas.RotateDegrees(checked((float)hatch.Tile.RotationDegrees), bounds.MidX, bounds.MidY);
            using SKPaint foreground = CreatePaint(hatch.Foreground, opacity, SKPaintStyle.Stroke);
            foreground.StrokeWidth = checked((float)hatch.Tile.LineWidthMm);
            foreground.IsAntialias = true;
            for (double y = bounds.Top - padding; y <= bounds.Bottom + padding; y += tileHeight)
            {
                for (double x = bounds.Left - padding; x <= bounds.Right + padding; x += tileWidth)
                {
                    foreach (HatchPrimitive primitive in hatch.Tile.Primitives)
                    {
                        switch (primitive)
                        {
                            case HatchLine line:
                                canvas.DrawLine(
                                    checked((float)(x + line.Start.X)),
                                    checked((float)(y + line.Start.Y)),
                                    checked((float)(x + line.End.X)),
                                    checked((float)(y + line.End.Y)),
                                    foreground);
                                break;
                            case HatchDot dot:
                                foreground.Style = SKPaintStyle.Fill;
                                canvas.DrawCircle(
                                    checked((float)(x + dot.Center.X)),
                                    checked((float)(y + dot.Center.Y)),
                                    checked((float)dot.RadiusMm),
                                    foreground);
                                foreground.Style = SKPaintStyle.Stroke;
                                break;
                        }
                    }
                }
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static void DrawStroke(SKCanvas canvas, SKPath path, RenderStroke stroke, double opacity)
    {
        if (!stroke.IsEnabled || stroke.WidthMm <= 0)
        {
            return;
        }

        using SKPaint paint = CreatePaint(stroke.Color, opacity, SKPaintStyle.Stroke);
        paint.StrokeWidth = checked((float)stroke.WidthMm);
        paint.StrokeCap = stroke.LineCap switch
        {
            StrokeLineCap.Round => SKStrokeCap.Round,
            StrokeLineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt,
        };
        paint.StrokeJoin = stroke.LineJoin switch
        {
            StrokeLineJoin.Bevel => SKStrokeJoin.Bevel,
            StrokeLineJoin.Round => SKStrokeJoin.Round,
            _ => SKStrokeJoin.Miter,
        };
        if (stroke.DashSegmentsMm.Count > 0)
        {
            paint.PathEffect = SKPathEffect.CreateDash(stroke.DashSegmentsMm.Select(checkedValue => checked((float)checkedValue)).ToArray(), 0);
        }

        canvas.DrawPath(path, paint);
    }

    private void DrawText(SKCanvas canvas, RenderTextCommand command, double opacity)
    {
        DrawGlyphRuns(canvas, command.GlyphRuns, command.Style, opacity);
    }

    private void DrawGlyphRuns(
        SKCanvas canvas,
        IReadOnlyList<RenderGlyphRun> runs,
        RenderTextStyle style,
        double opacity)
    {
        foreach (RenderGlyphRun run in runs)
        {
            FontResolution resolution = fontCatalog.Resolve(run.FontFace);
            using SKTypeface typeface = fontCatalog.OpenTypeface(resolution, style.IsBold, style.IsItalic);
            using SKFont font = new(typeface, checked((float)(run.FontSizePoints * PointsToMillimetres)))
            {
                LinearMetrics = true,
                Subpixel = true,
            };
            ushort[] glyphIds = run.Glyphs.Select(static glyph => checked((ushort)glyph.GlyphId)).ToArray();
            SKPoint[] positions = run.Glyphs.Select(static glyph => new SKPoint(
                checked((float)glyph.Origin.X),
                checked((float)glyph.Origin.Y))).ToArray();
            using SKTextBlobBuilder builder = new();
            builder.AddPositionedRun(glyphIds, font, positions);
            using SKTextBlob? blob = builder.Build();
            if (blob is not null)
            {
                using SKPaint paint = CreatePaint(run.Color, opacity, SKPaintStyle.Fill);
                canvas.DrawText(blob, 0, 0, paint);
            }

            if (style.IsUnderline && run.Bounds.Width > 0)
            {
                using SKPaint underline = CreatePaint(run.Color, opacity, SKPaintStyle.Stroke);
                underline.StrokeWidth = checked((float)Math.Max(0.08, run.FontSizePoints * PointsToMillimetres / 16));
                double y = run.Bounds.Bottom - underline.StrokeWidth;
                canvas.DrawLine(
                    checked((float)run.Bounds.X),
                    checked((float)y),
                    checked((float)run.Bounds.Right),
                    checked((float)y),
                    underline);
            }
        }
    }

    private static void DrawImage(
        SKCanvas canvas,
        RenderImageCommand command,
        double opacity,
        IRenderAssetProvider? assetProvider,
        SkiaRenderOptions options)
    {
        using SKPath borderPath = RectanglePath(command.LocalBounds);
        DrawFill(canvas, borderPath, command.Fill, opacity);
        using DecodedImage? decoded = assetProvider?.Load(command.Image, options.ImageSourceDpi);
        if (decoded is null)
        {
            if (!options.IgnoreMissingAssets)
            {
                throw new FileNotFoundException("Render image asset could not be resolved.");
            }
        }
        else
        {
            ImageLayoutResult layout = ImageLayoutService.Layout(
                decoded.Width,
                decoded.Height,
                command.LocalBounds,
                command.Image.FitMode,
                command.Image.Crop,
                options.ImageSourceDpi);
            using SKImage image = SKImage.FromBitmap(decoded.Bitmap);
            using SKPaint imagePaint = new()
            {
                Color = new SKColor(255, 255, 255, OpacityByte(opacity)),
                IsAntialias = true,
            };
            canvas.DrawImage(
                image,
                ToRect(layout.SourcePixels),
                ToRect(layout.DestinationMm),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
                imagePaint);
        }

        DrawStroke(canvas, borderPath, command.Border, opacity);
    }

    private void DrawBarcode(
        SKCanvas canvas,
        RenderBarcodeCommand command,
        double opacity,
        double unitsPerMillimetre,
        IRenderAssetProvider? assetProvider,
        SkiaRenderOptions renderOptions)
    {
        BarcodeOptions barcodeOptions = new()
        {
            Symbology = command.Symbology,
            Content = command.Content,
            QuietZoneMm = command.QuietZoneMm,
            ErrorCorrectionLevel = command.ErrorCorrectionLevel,
            ShowHumanReadableText = command.ShowHumanReadableText,
        };
        BarcodeMatrix matrix = barcodeService.Encode(barcodeOptions);
        double textHeight = command.ShowHumanReadableText && command.Symbology is not BarcodeSymbology.QrCode and not BarcodeSymbology.DataMatrix
            ? Math.Min(command.LocalBounds.Height * 0.25, command.HumanReadableTextStyle.FontSizePoints * PointsToMillimetres * 1.6)
            : 0;
        MmRect symbolBounds = new(
            command.LocalBounds.X + command.QuietZoneMm,
            command.LocalBounds.Y + command.QuietZoneMm,
            Math.Max(0, command.LocalBounds.Width - command.QuietZoneMm * 2),
            Math.Max(0, command.LocalBounds.Height - command.QuietZoneMm * 2 - textHeight));
        if (symbolBounds.Width <= 0 || symbolBounds.Height <= 0)
        {
            throw new InvalidOperationException("Barcode bounds are too small for the configured quiet zone.");
        }

        using SKPaint white = CreatePaint(RgbaColor.White, opacity, SKPaintStyle.Fill);
        using SKPaint black = CreatePaint(RgbaColor.Black, opacity, SKPaintStyle.Fill);
        black.IsAntialias = false;
        canvas.DrawRect(ToRect(command.LocalBounds), white);
        bool squareModules = command.Symbology is BarcodeSymbology.QrCode or BarcodeSymbology.DataMatrix;
        double moduleWidth = symbolBounds.Width / matrix.Width;
        double moduleHeight = symbolBounds.Height / matrix.Height;
        if (squareModules)
        {
            double module = Math.Min(moduleWidth, moduleHeight);
            moduleWidth = module;
            moduleHeight = module;
        }

        if (unitsPerMillimetre >= 8)
        {
            moduleWidth = Math.Max(1, Math.Floor(moduleWidth * unitsPerMillimetre)) / unitsPerMillimetre;
            if (squareModules)
            {
                moduleHeight = moduleWidth;
            }
        }

        double renderedWidth = matrix.Width * moduleWidth;
        double renderedHeight = matrix.Height * moduleHeight;
        double originX = symbolBounds.X + (symbolBounds.Width - renderedWidth) / 2;
        double originY = symbolBounds.Y + (symbolBounds.Height - renderedHeight) / 2;
        if (unitsPerMillimetre >= 8)
        {
            originX = Math.Round(originX * unitsPerMillimetre) / unitsPerMillimetre;
            originY = Math.Round(originY * unitsPerMillimetre) / unitsPerMillimetre;
        }
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                if (matrix[x, y])
                {
                    canvas.DrawRect(
                        checked((float)(originX + x * moduleWidth)),
                        checked((float)(originY + y * moduleHeight)),
                        checked((float)moduleWidth),
                        checked((float)moduleHeight),
                        black);
                }
            }
        }

        if (command.Symbology == BarcodeSymbology.QrCode && command.CenterIcon is not null)
        {
            DrawBarcodeCenterIcon(
                canvas,
                command,
                originX,
                originY,
                renderedWidth,
                renderedHeight,
                Math.Min(moduleWidth, moduleHeight),
                opacity,
                assetProvider,
                renderOptions);
        }

        if (textHeight > 0)
        {
            MmRect textBounds = new(
                command.LocalBounds.X,
                command.LocalBounds.Bottom - textHeight,
                command.LocalBounds.Width,
                textHeight);
            TextLayoutResult textLayout = textLayoutService.Layout(new TextLayoutRequest
            {
                Text = command.Content,
                Bounds = textBounds,
                Style = command.HumanReadableTextStyle with
                {
                    HorizontalAlignment = TextHorizontalAlignment.Center,
                    VerticalAlignment = TextVerticalAlignment.Center,
                    Wrap = false,
                    OverflowMode = TextOverflowMode.AutoFit,
                },
                MinimumAutoFitPoints = 4,
            });
            IReadOnlyList<RenderGlyphRun> shifted = textLayout.GlyphRuns.Select(run => run with
            {
                Glyphs = run.Glyphs.Select(glyph => glyph with
                {
                    Origin = glyph.Origin.Translate(textBounds.X, textBounds.Y),
                }).ToArray(),
                Bounds = run.Bounds.Translate(textBounds.X, textBounds.Y),
            }).ToArray();
            DrawGlyphRuns(canvas, shifted, command.HumanReadableTextStyle, opacity);
        }
    }

    /// <summary>在二维码模块中心绘制白色保护区和按比例 contain 的图标，保证位图、PDF 与打印使用同一几何。</summary>
    private static void DrawBarcodeCenterIcon(
        SKCanvas canvas,
        RenderBarcodeCommand command,
        double originX,
        double originY,
        double renderedWidth,
        double renderedHeight,
        double moduleSize,
        double opacity,
        IRenderAssetProvider? assetProvider,
        SkiaRenderOptions options)
    {
        double symbolSide = Math.Min(renderedWidth, renderedHeight);
        double iconSide = symbolSide * Math.Clamp(
            command.CenterIconScale,
            BarcodeElement.MinimumCenterIconScale,
            BarcodeElement.MaximumCenterIconScale);
        double protectionSide = Math.Min(symbolSide * 0.30, iconSide + moduleSize * 2);
        double centerX = originX + renderedWidth / 2;
        double centerY = originY + renderedHeight / 2;
        MmRect protectionBounds = new(
            centerX - protectionSide / 2,
            centerY - protectionSide / 2,
            protectionSide,
            protectionSide);
        using (SKPaint protection = CreatePaint(RgbaColor.White, opacity, SKPaintStyle.Fill))
        {
            canvas.DrawRect(ToRect(protectionBounds), protection);
        }

        using DecodedImage? decoded = assetProvider?.Load(command.CenterIcon!, options.ImageSourceDpi);
        if (decoded is null)
        {
            if (!options.IgnoreMissingAssets)
            {
                throw new FileNotFoundException("QR center icon asset could not be resolved.");
            }
            return;
        }

        MmRect iconBounds = new(centerX - iconSide / 2, centerY - iconSide / 2, iconSide, iconSide);
        ImageLayoutResult layout = ImageLayoutService.Layout(
            decoded.Width,
            decoded.Height,
            iconBounds,
            ImageFitMode.Contain,
            crop: null,
            options.ImageSourceDpi);
        using SKImage image = SKImage.FromBitmap(decoded.Bitmap);
        using SKPaint paint = new()
        {
            Color = new SKColor(255, 255, 255, OpacityByte(opacity)),
            IsAntialias = true,
        };
        canvas.DrawImage(
            image,
            ToRect(layout.SourcePixels),
            ToRect(layout.DestinationMm),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            paint);
    }

    private static SKPath RectanglePath(MmRect rectangle)
    {
        using SKPathBuilder builder = new();
        builder.AddRect(ToRect(rectangle));
        return builder.Detach();
    }

    private static SKPath ToPath(RenderPath path)
    {
        using SKPathBuilder builder = new()
        {
            FillType = path.FillRule == RenderFillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding,
        };
        foreach (RenderPathSegment segment in path.Segments)
        {
            switch (segment)
            {
                case RenderMoveTo move:
                    builder.MoveTo(checked((float)move.Point.X), checked((float)move.Point.Y));
                    break;
                case RenderLineTo line:
                    builder.LineTo(checked((float)line.Point.X), checked((float)line.Point.Y));
                    break;
                case RenderCubicTo cubic:
                    builder.CubicTo(
                        checked((float)cubic.Control1.X),
                        checked((float)cubic.Control1.Y),
                        checked((float)cubic.Control2.X),
                        checked((float)cubic.Control2.Y),
                        checked((float)cubic.End.X),
                        checked((float)cubic.End.Y));
                    break;
                case RenderClosePath:
                    builder.Close();
                    break;
            }
        }

        return builder.Detach();
    }

    private static SKPaint CreatePaint(RgbaColor color, double opacity, SKPaintStyle style) => new()
    {
        Color = new SKColor(color.Red, color.Green, color.Blue, OpacityByte(opacity * color.Alpha / byte.MaxValue)),
        Style = style,
        IsAntialias = true,
    };

    private static byte OpacityByte(double opacity) =>
        checked((byte)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue, MidpointRounding.AwayFromZero));

    private static SKRect ToRect(MmRect rectangle) => new(
        checked((float)rectangle.X),
        checked((float)rectangle.Y),
        checked((float)rectangle.Right),
        checked((float)rectangle.Bottom));

    private static SKMatrix ToMatrix(RenderTransform transform) => new()
    {
        ScaleX = checked((float)transform.M11),
        SkewX = checked((float)transform.M21),
        TransX = checked((float)transform.OffsetX),
        SkewY = checked((float)transform.M12),
        ScaleY = checked((float)transform.M22),
        TransY = checked((float)transform.OffsetY),
        Persp0 = 0,
        Persp1 = 0,
        Persp2 = 1,
    };
}
