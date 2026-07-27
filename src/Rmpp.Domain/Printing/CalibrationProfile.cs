using Rmpp.Domain.Common;
using Rmpp.Domain.Geometry;

namespace Rmpp.Domain.Printing;

public sealed record CalibrationProfile
{
    public required PrinterMediaKey Key { get; init; }
    public MmPoint OffsetMm { get; init; }
    public double ScaleX { get; init; } = 1;
    public double ScaleY { get; init; } = 1;
    public double RotationDegrees { get; init; }
    public DateTimeOffset? LastVerifiedAt { get; init; }

    public CalibrationProfile Validate()
    {
        DomainGuard.InRange(ScaleX, 0.9, 1.1, nameof(ScaleX));
        DomainGuard.InRange(ScaleY, 0.9, 1.1, nameof(ScaleY));
        DomainGuard.InRange(RotationDegrees, -5, 5, nameof(RotationDegrees));
        DomainGuard.InRange(OffsetMm.X, -50, 50, nameof(OffsetMm));
        DomainGuard.InRange(OffsetMm.Y, -50, 50, nameof(OffsetMm));
        return this;
    }
}
