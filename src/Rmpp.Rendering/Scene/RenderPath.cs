using Rmpp.Domain.Geometry;

namespace Rmpp.Rendering.Scene;

public enum RenderFillRule
{
    NonZero,
    EvenOdd,
}

public abstract record RenderPathSegment;
public sealed record RenderMoveTo(MmPoint Point) : RenderPathSegment;
public sealed record RenderLineTo(MmPoint Point) : RenderPathSegment;
public sealed record RenderCubicTo(MmPoint Control1, MmPoint Control2, MmPoint End) : RenderPathSegment;
public sealed record RenderClosePath : RenderPathSegment;

/// <summary>由确定性毫米制Move、Line、Cubic和Close段组成的后端无关路径。</summary>
public sealed record RenderPath
{
    public required IReadOnlyList<RenderPathSegment> Segments { get; init; }
    public RenderFillRule FillRule { get; init; } = RenderFillRule.NonZero;

    public RenderPath Validate()
    {
        if (Segments.Count == 0 || Segments[0] is not RenderMoveTo)
        {
            throw new InvalidOperationException("A render path must begin with MoveTo.");
        }

        return this;
    }
}

/// <summary>按固定顺序构造不可变渲染路径。</summary>
public sealed class RenderPathBuilder
{
    private readonly List<RenderPathSegment> segments = [];

    public RenderPathBuilder MoveTo(MmPoint point)
    {
        segments.Add(new RenderMoveTo(point));
        return this;
    }

    public RenderPathBuilder LineTo(MmPoint point)
    {
        segments.Add(new RenderLineTo(point));
        return this;
    }

    public RenderPathBuilder CubicTo(MmPoint control1, MmPoint control2, MmPoint end)
    {
        segments.Add(new RenderCubicTo(control1, control2, end));
        return this;
    }

    public RenderPathBuilder Close()
    {
        segments.Add(new RenderClosePath());
        return this;
    }

    public RenderPath Build(RenderFillRule fillRule = RenderFillRule.NonZero) =>
        new RenderPath { Segments = segments.ToArray(), FillRule = fillRule }.Validate();
}
