using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rmpp.Application.Abstractions;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;
using Rmpp.Infrastructure.Pdf;

namespace Rmpp.Desktop.Controls;

public sealed class ElementSelectedEventArgs(Guid? elementId, bool isAdditive) : RoutedEventArgs
{
    public Guid? ElementId { get; } = elementId;
    public bool IsAdditive { get; } = isAdditive;
}

public sealed class ElementsMovedEventArgs(double deltaXmm, double deltaYmm) : RoutedEventArgs
{
    public double DeltaXmm { get; } = deltaXmm;
    public double DeltaYmm { get; } = deltaYmm;
}

public sealed class ElementResizedEventArgs(MmRect bounds) : RoutedEventArgs
{
    public MmRect Bounds { get; } = bounds;
}

public sealed class ElementCreateRequestedEventArgs(DesignerTool tool, MmRect bounds) : RoutedEventArgs
{
    public DesignerTool Tool { get; } = tool;
    public MmRect Bounds { get; } = bounds;
}

/// <summary>绘制完整物理页面、网格、参考线、元素和选择框，并把指针拖动换算回毫米。</summary>
public sealed class DesignerSurface : FrameworkElement
{
    public const string DesignerToolDataFormat = "RMPP.DesignerTool";
    private const double DipPerMm = 96d / 25.4;
    private const double MarginDip = 32;
    private static readonly PdfBackgroundRasterizer PdfRasterizer = new();
    private Point? dragStart;
    private MmRect? resizeStartBounds;
    private Point? creationStart;
    private Point? creationCurrent;
    private Dictionary<BackgroundImageKey, ImageSource> backgroundImages = [];
    private int backgroundRefreshVersion;

    public DesignerSurface()
    {
        AllowDrop = true;
    }

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(TemplateDocument), typeof(DesignerSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnBackgroundSourceChanged));
    public static readonly DependencyProperty SelectedElementIdsProperty = DependencyProperty.Register(
        nameof(SelectedElementIds), typeof(IReadOnlySet<Guid>), typeof(DesignerSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(DesignerSurface),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(DesignerSurface), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GridSpacingMmProperty = DependencyProperty.Register(
        nameof(GridSpacingMm), typeof(double), typeof(DesignerSurface), new FrameworkPropertyMetadata(5d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PrintableAreaProperty = DependencyProperty.Register(
        nameof(PrintableArea), typeof(MmRect?), typeof(DesignerSurface), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ActiveToolProperty = DependencyProperty.Register(
        nameof(ActiveTool), typeof(DesignerTool), typeof(DesignerSurface), new FrameworkPropertyMetadata(DesignerTool.Select));
    public static readonly DependencyProperty AssetContentsProperty = DependencyProperty.Register(
        nameof(AssetContents), typeof(IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>), typeof(DesignerSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnBackgroundSourceChanged));

    public TemplateDocument? Document { get => (TemplateDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public IReadOnlySet<Guid>? SelectedElementIds { get => (IReadOnlySet<Guid>?)GetValue(SelectedElementIdsProperty); set => SetValue(SelectedElementIdsProperty, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    public double GridSpacingMm { get => (double)GetValue(GridSpacingMmProperty); set => SetValue(GridSpacingMmProperty, value); }
    public MmRect? PrintableArea { get => (MmRect?)GetValue(PrintableAreaProperty); set => SetValue(PrintableAreaProperty, value); }
    public DesignerTool ActiveTool { get => (DesignerTool)GetValue(ActiveToolProperty); set => SetValue(ActiveToolProperty, value); }
    public IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? AssetContents
    {
        get => (IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>?)GetValue(AssetContentsProperty);
        set => SetValue(AssetContentsProperty, value);
    }

    public event EventHandler<ElementSelectedEventArgs>? ElementSelected;
    public event EventHandler<ElementsMovedEventArgs>? ElementsMoved;
    public event EventHandler<ElementResizedEventArgs>? ElementResized;
    public event EventHandler<ElementCreateRequestedEventArgs>? ElementCreateRequested;

    protected override Size MeasureOverride(Size availableSize)
    {
        MmSize size = Document?.Page.Media.Size ?? new MmSize(210, 297);
        return new Size(size.Width * DipPerMm * Zoom + MarginDip * 2, size.Height * DipPerMm * Zoom + MarginDip * 2);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Document is null)
        {
            return;
        }

        double scale = DipPerMm * Zoom;
        drawingContext.PushTransform(new TranslateTransform(MarginDip, MarginDip));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        MmSize pageSize = Document.Page.Media.Size;
        Brush pageBrush = SystemColors.WindowBrush;
        Brush borderBrush = SystemColors.WindowTextBrush;
        drawingContext.DrawRectangle(pageBrush, new Pen(borderBrush, 1 / scale), new Rect(0, 0, pageSize.Width, pageSize.Height));
        if (ShowGrid)
        {
            DrawGrid(drawingContext, pageSize, scale, GridSpacingMm);
        }
        if (PrintableArea is { } printable)
        {
            drawingContext.DrawRectangle(null, new Pen(Brushes.OrangeRed, 1 / scale) { DashStyle = DashStyles.Dash },
                new Rect(printable.X, printable.Y, printable.Width, printable.Height));
        }
        foreach (BackgroundDefinition background in Document.Backgrounds.Where(static background => background.IsVisible))
        {
            BackgroundImageKey key = new(background.AssetId, background.PdfPageNumber);
            if (backgroundImages.TryGetValue(key, out ImageSource? image))
            {
                DrawBackgroundImage(drawingContext, background, image);
                continue;
            }

            Brush placeholderBrush = background.IsPrintable
                ? new SolidColorBrush(Color.FromArgb(24, 76, 175, 80))
                : new SolidColorBrush(Color.FromArgb(24, 255, 152, 0));
            Pen placeholderPen = new(background.IsPrintable ? Brushes.ForestGreen : Brushes.DarkOrange, 1 / scale)
            {
                DashStyle = background.IsPrintable ? DashStyles.Solid : DashStyles.Dash,
            };
            drawingContext.DrawRectangle(placeholderBrush, placeholderPen,
                new Rect(background.Bounds.X, background.Bounds.Y, background.Bounds.Width, background.Bounds.Height));
        }
        foreach (GuideDefinition guide in Document.Guides)
        {
            Pen guidePen = new(SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.DeepSkyBlue, 1 / scale);
            if (guide.Orientation == GuideOrientation.Vertical)
            {
                drawingContext.DrawLine(guidePen, new Point(guide.PositionMm, 0), new Point(guide.PositionMm, pageSize.Height));
            }
            else
            {
                drawingContext.DrawLine(guidePen, new Point(0, guide.PositionMm), new Point(pageSize.Width, guide.PositionMm));
            }
        }

        HashSet<Guid> visibleLayers = Document.Layers.Where(static layer => layer.IsVisible).Select(static layer => layer.Id).ToHashSet();
        foreach (TemplateElement element in Document.Elements.Where(element => element.IsVisible && visibleLayers.Contains(element.LayerId)).OrderBy(static element => element.ZIndex))
        {
            DrawElement(drawingContext, element, scale);
        }

        if (creationStart is { } start && creationCurrent is { } current)
        {
            MmRect preview = CreateBounds(start, current, ActiveTool, useDefaultWhenClick: false);
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(24, 30, 144, 255)),
                new Pen(Brushes.DodgerBlue, 1 / scale) { DashStyle = DashStyles.Dash },
                new Rect(preview.X, preview.Y, preview.Width, preview.Height));
        }

        drawingContext.Pop();
        drawingContext.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        if (ActiveTool != DesignerTool.Select)
        {
            creationStart = e.GetPosition(this);
            creationCurrent = creationStart;
            e.Handled = true;
            return;
        }

        dragStart = e.GetPosition(this);
        MmPoint point = ToMillimetres(dragStart.Value);
        Guid? hit = Document is null ? null : ElementHitTester.HitTest(Document, point, 4 / Math.Max(Zoom, 0.1))?.Id;
        TemplateElement? selected = Document?.Elements.FirstOrDefault(element => SelectedElementIds?.Contains(element.Id) == true);
        if (selected is not null && IsNearBottomRight(point, selected.Bounds, 5 / (DipPerMm * Math.Max(Zoom, 0.1))))
        {
            resizeStartBounds = selected.Bounds;
        }
        ElementSelected?.Invoke(this, new ElementSelectedEventArgs(hit, Keyboard.Modifiers.HasFlag(ModifierKeys.Control)));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (creationStart is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            creationCurrent = e.GetPosition(this);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (creationStart is { } createStart)
        {
            MmRect bounds = CreateBounds(createStart, e.GetPosition(this), ActiveTool, useDefaultWhenClick: true);
            ElementCreateRequested?.Invoke(this, new ElementCreateRequestedEventArgs(ActiveTool, bounds));
            creationStart = null;
            creationCurrent = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (dragStart is { } start)
        {
            Point end = e.GetPosition(this);
            double scale = DipPerMm * Math.Max(Zoom, 0.1);
            double dx = (end.X - start.X) / scale;
            double dy = (end.Y - start.Y) / scale;
            if (resizeStartBounds is { } original)
            {
                ElementResized?.Invoke(this, new ElementResizedEventArgs(new MmRect(
                    original.X, original.Y, Math.Max(0.1, original.Width + dx), Math.Max(0.1, original.Height + dy))));
            }
            else if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
            {
                ElementsMoved?.Invoke(this, new ElementsMovedEventArgs(dx, dy));
            }
        }

        dragStart = null;
        resizeStartBounds = null;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effects = TryGetDraggedTool(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (TryGetDraggedTool(e.Data, out DesignerTool tool) && tool != DesignerTool.Select)
        {
            Point point = e.GetPosition(this);
            MmRect bounds = CreateBounds(point, point, tool, useDefaultWhenClick: true);
            ElementCreateRequested?.Invoke(this, new ElementCreateRequestedEventArgs(tool, bounds));
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private MmPoint ToMillimetres(Point point)
    {
        double scale = DipPerMm * Math.Max(Zoom, 0.1);
        return new MmPoint((point.X - MarginDip) / scale, (point.Y - MarginDip) / scale);
    }

    private MmRect CreateBounds(Point startDip, Point endDip, DesignerTool tool, bool useDefaultWhenClick)
    {
        MmPoint start = ToMillimetres(startDip);
        MmPoint end = ToMillimetres(endDip);
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double width = Math.Abs(end.X - start.X);
        double height = Math.Abs(end.Y - start.Y);
        if (useDefaultWhenClick && width < 0.5 && height < 0.5)
        {
            (width, height) = GetDefaultSize(tool);
            x = start.X;
            y = start.Y;
        }

        MmSize page = Document?.Page.Media.Size ?? new MmSize(210, 297);
        width = Math.Clamp(width, 0.1, page.Width);
        height = Math.Clamp(height, 0.1, page.Height);
        x = Math.Clamp(x, 0, Math.Max(0, page.Width - width));
        y = Math.Clamp(y, 0, Math.Max(0, page.Height - height));
        return new MmRect(x, y, width, height);
    }

    private static (double Width, double Height) GetDefaultSize(DesignerTool tool) => tool switch
    {
        DesignerTool.Text or DesignerTool.DateTime or DesignerTool.Serial => (40, 12),
        DesignerTool.Barcode => (45, 22),
        DesignerTool.QrCode or DesignerTool.DataMatrix => (25, 25),
        DesignerTool.Line => (30, 10),
        DesignerTool.Image => (40, 30),
        _ => (30, 20),
    };

    private static bool TryGetDraggedTool(IDataObject data, out DesignerTool tool)
    {
        string? value = data.GetDataPresent(DesignerToolDataFormat)
            ? data.GetData(DesignerToolDataFormat) as string
            : null;
        return Enum.TryParse(value, ignoreCase: false, out tool);
    }

    private static void DrawGrid(DrawingContext context, MmSize size, double scale, double spacing)
    {
        Pen pen = new(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 1 / scale);
        spacing = Math.Clamp(spacing, 0.1, 100);
        for (double x = spacing; x < size.Width; x += spacing)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, size.Height));
        }
        for (double y = spacing; y < size.Height; y += spacing)
        {
            context.DrawLine(pen, new Point(0, y), new Point(size.Width, y));
        }
    }

    private static bool IsNearBottomRight(MmPoint point, MmRect bounds, double tolerance) =>
        Math.Abs(point.X - bounds.Right) <= tolerance && Math.Abs(point.Y - bounds.Bottom) <= tolerance;

    private void DrawElement(DrawingContext context, TemplateElement element, double scale)
    {
        Rect bounds = new(element.Bounds.X, element.Bounds.Y, element.Bounds.Width, element.Bounds.Height);
        Brush fill = SystemParameters.HighContrast ? Brushes.Transparent : element switch
        {
            TextElement or DateTimeElement or SerialElement => new SolidColorBrush(Color.FromArgb(28, 25, 118, 210)),
            BarcodeElement => new SolidColorBrush(Color.FromArgb(35, 0, 0, 0)),
            ImageElement => new SolidColorBrush(Color.FromArgb(35, 76, 175, 80)),
            _ => Brushes.Transparent,
        };
        context.DrawRectangle(fill, new Pen(SystemColors.WindowTextBrush, 0.25), bounds);
        if (SelectedElementIds?.Contains(element.Id) == true)
        {
            Brush selectionBrush = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Brushes.DodgerBlue;
            Pen selection = new(selectionBrush, 1.5 / scale) { DashStyle = DashStyles.Dash };
            context.DrawRectangle(null, selection, bounds);
            double handle = 5 / scale;
            foreach (Point point in new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomLeft, bounds.BottomRight })
            {
                context.DrawRectangle(SystemColors.WindowBrush, new Pen(selectionBrush, 1 / scale), new Rect(point.X - handle / 2, point.Y - handle / 2, handle, handle));
            }
        }
    }

    private static void DrawBackgroundImage(
        DrawingContext context,
        BackgroundDefinition background,
        ImageSource image)
    {
        Rect target = new(background.Bounds.X, background.Bounds.Y, background.Bounds.Width, background.Bounds.Height);
        double sourceWidth = Math.Max(1, image.Width);
        double sourceHeight = Math.Max(1, image.Height);
        Rect destination = background.FitMode switch
        {
            ImageFitMode.Stretch => target,
            ImageFitMode.Cover => FitRectangle(target, sourceWidth, sourceHeight, contain: false),
            ImageFitMode.OriginalSize => new Rect(
                target.X,
                target.Y,
                sourceWidth / 96 * 25.4,
                sourceHeight / 96 * 25.4),
            _ => FitRectangle(target, sourceWidth, sourceHeight, contain: true),
        };
        context.PushClip(new RectangleGeometry(target));
        context.PushOpacity(background.Opacity);
        context.DrawImage(image, destination);
        context.Pop();
        context.Pop();
    }

    private static Rect FitRectangle(Rect target, double sourceWidth, double sourceHeight, bool contain)
    {
        double scale = contain
            ? Math.Min(target.Width / sourceWidth, target.Height / sourceHeight)
            : Math.Max(target.Width / sourceWidth, target.Height / sourceHeight);
        double width = sourceWidth * scale;
        double height = sourceHeight * scale;
        return new Rect(
            target.X + (target.Width - width) / 2,
            target.Y + (target.Height - height) / 2,
            width,
            height);
    }

    private static void OnBackgroundSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((DesignerSurface)sender).RefreshBackgroundImages();

    /// <summary>在 UI 线程外栅格化 PDF，并用版本号丢弃过期加载结果，避免快速切换文档时串用背景。</summary>
    private async void RefreshBackgroundImages()
    {
        int version = ++backgroundRefreshVersion;
        TemplateDocument? document = Document;
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? contents = AssetContents;
        if (document is null || contents is null)
        {
            backgroundImages = [];
            InvalidateVisual();
            return;
        }

        Dictionary<Guid, AssetReference> references = document.Assets.ToDictionary(static asset => asset.Id);
        Dictionary<BackgroundImageKey, ImageSource> loaded = [];
        foreach (BackgroundDefinition background in document.Backgrounds)
        {
            BackgroundImageKey key = new(background.AssetId, background.PdfPageNumber);
            if (loaded.ContainsKey(key)
                || !references.TryGetValue(background.AssetId, out AssetReference? reference)
                || !contents.TryGetValue(background.AssetId, out ReadOnlyMemory<byte> bytes))
            {
                continue;
            }

            try
            {
                loaded[key] = reference.MediaType == "application/pdf"
                    ? await RasterizePdfAsync(background, bytes).ConfigureAwait(true)
                    : LoadBitmap(bytes);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException)
            {
                // 验证层会报告损坏或缺失资源；画布保留背景占位框，继续允许用户修复模板。
            }
        }

        if (version == backgroundRefreshVersion)
        {
            backgroundImages = loaded;
            InvalidateVisual();
        }
    }

    private static BitmapImage LoadBitmap(ReadOnlyMemory<byte> bytes)
    {
        using MemoryStream stream = new(bytes.ToArray(), writable: false);
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static async Task<BitmapSource> RasterizePdfAsync(
        BackgroundDefinition background,
        ReadOnlyMemory<byte> bytes)
    {
        const double previewDpi = 144;
        int width = Math.Clamp((int)Math.Ceiling(background.Bounds.Width / 25.4 * previewDpi), 1, 4096);
        int height = Math.Clamp((int)Math.Ceiling(background.Bounds.Height / 25.4 * previewDpi), 1, 4096);
        PdfRasterizedPage page = await PdfRasterizer.RasterizeAsync(new PdfRasterizationRequest
        {
            DocumentBytes = bytes,
            PageNumber = background.PdfPageNumber,
            TargetWidthPixels = width,
            TargetHeightPixels = height,
        }).ConfigureAwait(false);
        BitmapSource bitmap = BitmapSource.Create(
            page.Width,
            page.Height,
            previewDpi,
            previewDpi,
            PixelFormats.Bgra32,
            null,
            page.Pixels.ToArray(),
            page.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    private readonly record struct BackgroundImageKey(Guid AssetId, int PdfPageNumber);
}
