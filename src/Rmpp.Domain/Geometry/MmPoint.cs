using Rmpp.Domain.Common;

namespace Rmpp.Domain.Geometry;

public readonly record struct MmPoint
{
    public MmPoint(double x, double y)
    {
        X = DomainGuard.Finite(x, nameof(x));
        Y = DomainGuard.Finite(y, nameof(y));
    }

    public double X { get; }

    public double Y { get; }

    public MmPoint Translate(double deltaX, double deltaY) => new(X + deltaX, Y + deltaY);
}
