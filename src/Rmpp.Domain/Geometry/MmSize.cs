using Rmpp.Domain.Common;

namespace Rmpp.Domain.Geometry;

public readonly record struct MmSize
{
    public MmSize(double width, double height)
    {
        Width = DomainGuard.NonNegative(width, nameof(width));
        Height = DomainGuard.NonNegative(height, nameof(height));
    }

    public double Width { get; }

    public double Height { get; }

    public bool IsEmpty => Width == 0 || Height == 0;
}
