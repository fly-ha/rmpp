using Rmpp.Domain.Common;

namespace Rmpp.Domain.Geometry;

public readonly record struct Angle
{
    public Angle(double degrees)
    {
        DomainGuard.Finite(degrees, nameof(degrees));
        Degrees = ((degrees % 360) + 360) % 360;
    }

    public double Degrees { get; }

    public static Angle Zero => new(0);
}
