namespace Rmpp.Application.Printing;

/// <summary>定义全部、连续范围或显式记录索引选择；索引从零开始。</summary>
public sealed record PrintRecordSelection
{
    public int? StartIndex { get; init; }
    public int? EndIndexInclusive { get; init; }
    public IReadOnlyList<int>? ExplicitIndices { get; init; }

    public static PrintRecordSelection All { get; } = new();

    public IReadOnlyList<int> Resolve(int availableRecords)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(availableRecords);
        if (ExplicitIndices is not null)
        {
            int[] explicitIndices = ExplicitIndices.Distinct().ToArray();
            if (explicitIndices.Any(index => index < 0 || index >= availableRecords))
            {
                throw new InvalidOperationException("Selected record is outside the data set.");
            }

            return explicitIndices;
        }

        if (availableRecords == 0)
        {
            return Array.Empty<int>();
        }

        int start = StartIndex ?? 0;
        int end = EndIndexInclusive ?? availableRecords - 1;
        if (start < 0 || end < start || end >= availableRecords)
        {
            throw new InvalidOperationException("Selected record range is invalid.");
        }

        return Enumerable.Range(start, end - start + 1).ToArray();
    }
}
