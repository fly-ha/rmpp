namespace Rmpp.Domain.Data;

public sealed record SerialDefinition
{
    public long Start { get; init; } = 1;
    public long Step { get; init; } = 1;
    public int MinimumDigits { get; init; }
    public string Prefix { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;
    public bool AdvancePerCopy { get; init; }

    public SerialDefinition Validate()
    {
        if (Step == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Step), "Serial step cannot be zero.");
        }

        if (MinimumDigits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumDigits));
        }

        return this;
    }
}
