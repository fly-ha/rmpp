namespace Rmpp.Domain.Styles;

public sealed record DashPattern
{
    public DashPattern(IReadOnlyList<double>? segments = null)
    {
        Segments = (segments ?? Array.Empty<double>()).ToArray();
        if (Segments.Any(static value => !double.IsFinite(value) || value <= 0))
        {
            throw new ArgumentException("Dash segments must be positive finite values.", nameof(segments));
        }
    }

    public IReadOnlyList<double> Segments { get; }

    public static DashPattern Solid { get; } = new();
}
