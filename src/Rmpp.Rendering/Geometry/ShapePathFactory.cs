using Rmpp.Domain.Elements;
using Rmpp.Domain.Geometry;
using Rmpp.Rendering.Scene;

namespace Rmpp.Rendering.Geometry;

/// <summary>把全部领域图形转换为元素局部毫米坐标中的确定性路径。</summary>
public static class ShapePathFactory
{
    private const double CircleKappa = 0.5522847498307936;

    public static RenderPath Create(ShapeElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Validate();
        return element switch
        {
            LineElement line => CreateLine(line),
            RectangleElement rectangle => CreateRectangle(rectangle),
            EllipseElement ellipse => CreateEllipse(ellipse.Bounds.Size),
            ArcElement arc => ArcGeometryBuilder.BuildArc(LocalBounds(arc.Bounds), arc.StartAngle, arc.SweepDegrees),
            SectorElement sector => ArcGeometryBuilder.BuildSector(LocalBounds(sector.Bounds), sector.StartAngle, sector.SweepDegrees),
            PolylineElement polyline => PolylineGeometryBuilder.Build(polyline.Points),
            PolygonElement polygon => PolygonGeometryBuilder.Build(polygon.Points),
            _ => throw new NotSupportedException($"Unsupported shape element: {element.GetType().Name}"),
        };
    }

    private static RenderPath CreateLine(LineElement line)
    {
        if (line.Start == line.End)
        {
            throw new InvalidOperationException("A line requires different start and end points.");
        }

        return new RenderPathBuilder().MoveTo(line.Start).LineTo(line.End).Build();
    }

    private static RenderPath CreateRectangle(RectangleElement rectangle)
    {
        double width = rectangle.Bounds.Width;
        double height = rectangle.Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("A rectangle requires positive bounds.");
        }

        double radiusX = Math.Min(rectangle.CornerRadius.Width, width / 2);
        double radiusY = Math.Min(rectangle.CornerRadius.Height, height / 2);
        if (radiusX == 0 || radiusY == 0)
        {
            return new RenderPathBuilder()
                .MoveTo(new MmPoint(0, 0))
                .LineTo(new MmPoint(width, 0))
                .LineTo(new MmPoint(width, height))
                .LineTo(new MmPoint(0, height))
                .Close()
                .Build();
        }

        double controlX = radiusX * CircleKappa;
        double controlY = radiusY * CircleKappa;
        return new RenderPathBuilder()
            .MoveTo(new MmPoint(radiusX, 0))
            .LineTo(new MmPoint(width - radiusX, 0))
            .CubicTo(new MmPoint(width - radiusX + controlX, 0), new MmPoint(width, radiusY - controlY), new MmPoint(width, radiusY))
            .LineTo(new MmPoint(width, height - radiusY))
            .CubicTo(new MmPoint(width, height - radiusY + controlY), new MmPoint(width - radiusX + controlX, height), new MmPoint(width - radiusX, height))
            .LineTo(new MmPoint(radiusX, height))
            .CubicTo(new MmPoint(radiusX - controlX, height), new MmPoint(0, height - radiusY + controlY), new MmPoint(0, height - radiusY))
            .LineTo(new MmPoint(0, radiusY))
            .CubicTo(new MmPoint(0, radiusY - controlY), new MmPoint(radiusX - controlX, 0), new MmPoint(radiusX, 0))
            .Close()
            .Build();
    }

    private static RenderPath CreateEllipse(MmSize size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new InvalidOperationException("An ellipse requires positive bounds.");
        }

        MmRect bounds = new(0, 0, size.Width, size.Height);
        RenderPathBuilder builder = new();
        builder.MoveTo(ArcGeometryBuilder.PointAt(bounds, 0));
        ArcGeometryBuilder.AppendArc(builder, bounds, 0, 360);
        return builder.Close().Build();
    }

    private static MmRect LocalBounds(MmRect bounds) => new(0, 0, bounds.Width, bounds.Height);
}
