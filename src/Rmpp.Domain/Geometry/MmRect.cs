using Rmpp.Domain.Common;

namespace Rmpp.Domain.Geometry;

public readonly record struct MmRect
{
    public MmRect(double x, double y, double width, double height)
    {
        X = DomainGuard.Finite(x, nameof(x));
        Y = DomainGuard.Finite(y, nameof(y));
        Width = DomainGuard.NonNegative(width, nameof(width));
        Height = DomainGuard.NonNegative(height, nameof(height));
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public MmPoint Location => new(X, Y);

    public MmSize Size => new(Width, Height);

    public MmRect Translate(double deltaX, double deltaY) => new(X + deltaX, Y + deltaY, Width, Height);
}
