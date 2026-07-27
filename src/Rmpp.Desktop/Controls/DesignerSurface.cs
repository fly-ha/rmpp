using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Rmpp.Domain.Documents;
using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;

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

/// <summary>绘制完整物理页面、网格、参考线、元素和选择框，并把指针拖动换算回毫米。</summary>
public sealed class DesignerSurface : FrameworkElement
{
    private const double DipPerMm = 96d / 25.4;
    private const double MarginDip = 32;
    private Point? dragStart;
    private MmRect? resizeStartBounds;

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(TemplateDocument), typeof(DesignerSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
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

    public TemplateDocument? Document { get => (TemplateDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }
    public IReadOnlySet<Guid>? SelectedElementIds { get => (IReadOnlySet<Guid>?)GetValue(SelectedElementIdsProperty); set => SetValue(SelectedElementIdsProperty, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    public double GridSpacingMm { get => (double)GetValue(GridSpacingMmProperty); set => SetValue(GridSpacingMmProperty, value); }
    public MmRect? PrintableArea { get => (MmRect?)GetValue(PrintableAreaProperty); set => SetValue(PrintableAreaProperty, value); }

    public event EventHandler<ElementSelectedEventArgs>? ElementSelected;
    public event EventHandler<ElementsMovedEventArgs>? ElementsMoved;
    public event EventHandler<ElementResizedEventArgs>? ElementResized;

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
            Brush backgroundBrush = background.IsPrintable
                ? new SolidColorBrush(Color.FromArgb(24, 76, 175, 80))
                : new SolidColorBrush(Color.FromArgb(24, 255, 152, 0));
            Pen backgroundPen = new(background.IsPrintable ? Brushes.ForestGreen : Brushes.DarkOrange, 1 / scale)
            {
                DashStyle = background.IsPrintable ? DashStyles.Solid : DashStyles.Dash,
            };
            drawingContext.DrawRectangle(backgroundBrush, backgroundPen,
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

        drawingContext.Pop();
        drawingContext.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
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

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
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

    private MmPoint ToMillimetres(Point point)
    {
        double scale = DipPerMm * Math.Max(Zoom, 0.1);
        return new MmPoint((point.X - MarginDip) / scale, (point.Y - MarginDip) / scale);
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
}
