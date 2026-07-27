using Rmpp.Domain.Common;

namespace Rmpp.Domain.Geometry;

public readonly record struct MmRange
{
    public MmRange(double minimum, double maximum)
    {
        Minimum = DomainGuard.Finite(minimum, nameof(minimum));
        Maximum = DomainGuard.Finite(maximum, nameof(maximum));
        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum cannot exceed maximum.", nameof(minimum));
        }
    }

    public double Minimum { get; }
    public double Maximum { get; }
    public double Length => Maximum - Minimum;
}
