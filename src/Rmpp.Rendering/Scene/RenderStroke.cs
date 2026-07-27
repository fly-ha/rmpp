using Rmpp.Domain.Styles;

namespace Rmpp.Rendering.Scene;

/// <summary>保存所有后端共享的描边颜色、宽度、虚线、端点和连接方式。</summary>
public sealed record RenderStroke
{
    public bool IsEnabled { get; init; }
    public RgbaColor Color { get; init; } = RgbaColor.Black;
    public double WidthMm { get; init; }
    public IReadOnlyList<double> DashSegmentsMm { get; init; } = Array.Empty<double>();
    public StrokeLineCap LineCap { get; init; }
    public StrokeLineJoin LineJoin { get; init; }

    public static RenderStroke FromDomain(StrokeStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        StrokeStyle validated = style.Validate();
        return new RenderStroke
        {
            IsEnabled = validated.IsEnabled,
            Color = validated.Color,
            WidthMm = validated.WidthMm,
            DashSegmentsMm = validated.DashPattern.Segments.ToArray(),
            LineCap = validated.LineCap,
            LineJoin = validated.LineJoin,
        };
    }
}
