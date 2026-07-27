using Rmpp.Domain.Documents;

namespace Rmpp.Application.Editing;

/// <summary>表示撤销或重做后应恢复的文档快照和修订身份。</summary>
public sealed record EditorHistorySnapshot(
    TemplateDocument Document,
    Guid Revision,
    string Description);

/// <summary>保存受条目数和估算内存双重限制的每文档撤销重做历史。</summary>
public sealed class UndoRedoManager
{
    private readonly int maximumEntries;
    private readonly long maximumEstimatedBytes;
    private readonly TimeSpan coalescingWindow;
    private readonly List<HistoryEntry> undoEntries = [];
    private readonly List<HistoryEntry> redoEntries = [];
    private long estimatedBytes;

    public UndoRedoManager(
        int maximumEntries = 200,
        long maximumEstimatedBytes = 64 * 1024 * 1024,
        TimeSpan? coalescingWindow = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEstimatedBytes);

        this.maximumEntries = maximumEntries;
        this.maximumEstimatedBytes = maximumEstimatedBytes;
        this.coalescingWindow = coalescingWindow ?? TimeSpan.FromMilliseconds(750);
    }

    public int UndoCount => undoEntries.Count;

    public int RedoCount => redoEntries.Count;

    public bool CanUndo => undoEntries.Count > 0;

    public bool CanRedo => redoEntries.Count > 0;

    public long EstimatedBytes => estimatedBytes;

    public void Record(
        TemplateDocument before,
        Guid beforeRevision,
        TemplateDocument after,
        Guid afterRevision,
        string description,
        string? coalescingKey,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ClearRedo();

        long entrySize = Estimate(before) + Estimate(after);
        if (coalescingKey is not null
            && undoEntries.LastOrDefault() is HistoryEntry previous
            && StringComparer.Ordinal.Equals(previous.CoalescingKey, coalescingKey)
            && timestamp - previous.Timestamp <= coalescingWindow)
        {
            estimatedBytes -= previous.EstimatedBytes;
            undoEntries[^1] = previous with
            {
                After = after,
                AfterRevision = afterRevision,
                Description = description,
                Timestamp = timestamp,
                EstimatedBytes = Estimate(previous.Before) + Estimate(after),
            };
            estimatedBytes += undoEntries[^1].EstimatedBytes;
        }
        else
        {
            undoEntries.Add(new HistoryEntry(
                before,
                beforeRevision,
                after,
                afterRevision,
                description,
                coalescingKey,
                timestamp,
                entrySize));
            estimatedBytes += entrySize;
        }

        TrimToBounds();
    }

    public EditorHistorySnapshot? Undo()
    {
        if (undoEntries.Count == 0)
        {
            return null;
        }

        HistoryEntry entry = undoEntries[^1];
        undoEntries.RemoveAt(undoEntries.Count - 1);
        redoEntries.Add(entry);
        estimatedBytes -= entry.EstimatedBytes;
        return new EditorHistorySnapshot(entry.Before, entry.BeforeRevision, entry.Description);
    }

    public EditorHistorySnapshot? Redo()
    {
        if (redoEntries.Count == 0)
        {
            return null;
        }

        HistoryEntry entry = redoEntries[^1];
        redoEntries.RemoveAt(redoEntries.Count - 1);
        undoEntries.Add(entry);
        estimatedBytes += entry.EstimatedBytes;
        TrimToBounds();
        return new EditorHistorySnapshot(entry.After, entry.AfterRevision, entry.Description);
    }

    public void Clear()
    {
        undoEntries.Clear();
        redoEntries.Clear();
        estimatedBytes = 0;
    }

    private void ClearRedo() => redoEntries.Clear();

    private void TrimToBounds()
    {
        while (undoEntries.Count > maximumEntries
               || estimatedBytes > maximumEstimatedBytes && undoEntries.Count > 1)
        {
            estimatedBytes -= undoEntries[0].EstimatedBytes;
            undoEntries.RemoveAt(0);
        }
    }

    private static long Estimate(TemplateDocument document)
    {
        long total = 1024;
        total += document.Layers.Count * 192L;
        total += document.Guides.Count * 96L;
        total += document.Backgrounds.Count * 256L;
        total += document.Assets.Count * 256L;
        total += document.Fields.Count * 192L;
        total += document.SampleValues.Count * 128L;
        total += document.Elements.Sum(static element =>
            512L + element.Name.Length * sizeof(char) +
            (element is Rmpp.Domain.Elements.PathElement path ? path.Points.Count * 32L : 0));
        return total;
    }

    private sealed record HistoryEntry(
        TemplateDocument Before,
        Guid BeforeRevision,
        TemplateDocument After,
        Guid AfterRevision,
        string Description,
        string? CoalescingKey,
        DateTimeOffset Timestamp,
        long EstimatedBytes);
}
