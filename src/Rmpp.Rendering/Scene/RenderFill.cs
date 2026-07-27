using Rmpp.Domain.Geometry;
using Rmpp.Domain.Styles;

namespace Rmpp.Rendering.Scene;

public abstract record RenderFill;
public sealed record RenderNoFill : RenderFill;
public sealed record RenderSolidFill(RgbaColor Color) : RenderFill;

public abstract record HatchPrimitive;
public sealed record HatchLine(MmPoint Start, MmPoint End) : HatchPrimitive;
public sealed record HatchDot(MmPoint Center, double RadiusMm) : HatchPrimitive;

/// <summary>表示可在形状局部空间重复平铺的毫米制底纹单元。</summary>
public sealed record HatchTile(
    MmSize Size,
    double RotationDegrees,
    double LineWidthMm,
    IReadOnlyList<HatchPrimitive> Primitives);

public sealed record RenderHatchFill(
    RgbaColor Foreground,
    RgbaColor Background,
    HatchPattern Pattern,
    HatchTile Tile) : RenderFill;
