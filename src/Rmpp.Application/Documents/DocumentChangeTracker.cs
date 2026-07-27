namespace Rmpp.Application.Documents;

/// <summary>用修订身份跟踪保存点，使撤销回保存状态时脏标记能够准确恢复。</summary>
public sealed class DocumentChangeTracker
{
    public DocumentChangeTracker(Guid? initialRevision = null)
    {
        CurrentRevision = initialRevision ?? Guid.NewGuid();
        SavedRevision = CurrentRevision;
    }

    public Guid CurrentRevision { get; private set; }

    public Guid SavedRevision { get; private set; }

    public bool IsDirty => CurrentRevision != SavedRevision;

    public Guid Advance()
    {
        CurrentRevision = Guid.NewGuid();
        return CurrentRevision;
    }

    public void MoveTo(Guid revision)
    {
        if (revision == Guid.Empty)
        {
            throw new ArgumentException("Revision cannot be empty.", nameof(revision));
        }

        CurrentRevision = revision;
    }

    public void MarkSaved() => SavedRevision = CurrentRevision;

    public void Reset(Guid? revision = null)
    {
        CurrentRevision = revision ?? Guid.NewGuid();
        SavedRevision = CurrentRevision;
    }
}
