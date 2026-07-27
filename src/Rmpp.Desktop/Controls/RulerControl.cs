using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rmpp.Desktop.Controls;

public sealed class RulerControl : FrameworkElement
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(RulerControl));
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(RulerControl), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(RenderSize));
        double spacing = 10 * 96 / 25.4 * Math.Max(Zoom, 0.1);
        Pen pen = new(Brushes.Gray, 1);
        double length = Orientation == Orientation.Horizontal ? RenderSize.Width : RenderSize.Height;
        for (double position = 32; position < length; position += spacing)
        {
            if (Orientation == Orientation.Horizontal)
            {
                drawingContext.DrawLine(pen, new Point(position, RenderSize.Height - 6), new Point(position, RenderSize.Height));
            }
            else
            {
                drawingContext.DrawLine(pen, new Point(RenderSize.Width - 6, position), new Point(RenderSize.Width, position));
            }
        }
    }
}
