using Rmpp.Domain.Documents;

namespace Rmpp.Application.Documents;

/// <summary>拥有一个文档的当前快照、资源、选择、保存修订和验证结果。</summary>
public sealed class DocumentSession
{
    private readonly DocumentValidator validator;
    private readonly DocumentChangeTracker changeTracker;

    public DocumentSession(
        TemplateDocument document,
        string? filePath = null,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? assetContents = null,
        bool isReadOnly = false,
        DocumentValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.validator = validator ?? new DocumentValidator();
        changeTracker = new DocumentChangeTracker();
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> frozenAssets = CopyAssets(assetContents);
        State = CreateState(document, filePath, isReadOnly, frozenAssets, new HashSet<Guid>());
    }

    public event EventHandler<DocumentSessionChangedEventArgs>? Changed;

    public DocumentSessionState State { get; private set; }

    public void SetSelection(IEnumerable<Guid> elementIds)
    {
        ArgumentNullException.ThrowIfNull(elementIds);
        HashSet<Guid> existing = State.Document.Elements.Select(static element => element.Id).ToHashSet();
        HashSet<Guid> selected = elementIds.Where(existing.Contains).ToHashSet();
        Publish(State with { SelectedElementIds = selected });
    }

    public void MarkSaved(string? filePath = null)
    {
        changeTracker.MarkSaved();
        Publish(State with
        {
            FilePath = filePath ?? State.FilePath,
            IsDirty = false,
            SavedRevision = changeTracker.SavedRevision,
        });
    }

    public void ImportAssets(IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        Dictionary<Guid, ReadOnlyMemory<byte>> merged = State.AssetContents.ToDictionary();
        foreach ((Guid id, ReadOnlyMemory<byte> bytes) in assets)
        {
            if (id == Guid.Empty || bytes.IsEmpty)
            {
                throw new ArgumentException("Imported assets require non-empty identities and content.", nameof(assets));
            }

            merged[id] = bytes.ToArray();
        }

        Publish(State with
        {
            AssetContents = merged,
            ValidationIssues = validator.Validate(State.Document, merged),
        });
    }

    internal Guid ApplyDocument(TemplateDocument document)
    {
        if (State.IsReadOnly)
        {
            throw new InvalidOperationException("The document session is read-only.");
        }

        Guid revision = changeTracker.Advance();
        ApplyDocument(document, revision);
        return revision;
    }

    internal void ApplyDocument(TemplateDocument document, Guid revision)
    {
        ArgumentNullException.ThrowIfNull(document);
        changeTracker.MoveTo(revision);
        HashSet<Guid> existing = document.Elements.Select(static element => element.Id).ToHashSet();
        IReadOnlySet<Guid> selected = State.SelectedElementIds.Where(existing.Contains).ToHashSet();
        Publish(CreateState(document, State.FilePath, State.IsReadOnly, State.AssetContents, selected));
    }

    private DocumentSessionState CreateState(
        TemplateDocument document,
        string? filePath,
        bool isReadOnly,
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> assets,
        IReadOnlySet<Guid> selected) =>
        new()
        {
            Document = document,
            FilePath = filePath,
            IsReadOnly = isReadOnly,
            IsDirty = changeTracker.IsDirty,
            CurrentRevision = changeTracker.CurrentRevision,
            SavedRevision = changeTracker.SavedRevision,
            SelectedElementIds = selected,
            AssetContents = assets,
            ValidationIssues = validator.Validate(document, assets),
        };

    private void Publish(DocumentSessionState state)
    {
        State = state;
        Changed?.Invoke(this, new DocumentSessionChangedEventArgs(state));
    }

    private static Dictionary<Guid, ReadOnlyMemory<byte>> CopyAssets(
        IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>>? source) =>
        source is null
            ? new Dictionary<Guid, ReadOnlyMemory<byte>>()
            : source.ToDictionary(static pair => pair.Key, static pair => (ReadOnlyMemory<byte>)pair.Value.ToArray());
}
