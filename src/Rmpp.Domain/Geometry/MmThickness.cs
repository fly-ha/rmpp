using Rmpp.Domain.Common;

namespace Rmpp.Domain.Geometry;

public readonly record struct MmThickness
{
    public MmThickness(double left, double top, double right, double bottom)
    {
        Left = DomainGuard.NonNegative(left, nameof(left));
        Top = DomainGuard.NonNegative(top, nameof(top));
        Right = DomainGuard.NonNegative(right, nameof(right));
        Bottom = DomainGuard.NonNegative(bottom, nameof(bottom));
    }

    public MmThickness(double uniform) : this(uniform, uniform, uniform, uniform)
    {
    }

    public double Left { get; }
    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }
}
