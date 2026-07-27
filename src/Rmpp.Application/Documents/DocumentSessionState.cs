using Rmpp.Application.Validation;
using Rmpp.Domain.Documents;

namespace Rmpp.Application.Documents;

/// <summary>保存单个文档标签页的不可变应用状态，避免不同标签页共享编辑历史或选择。</summary>
public sealed record DocumentSessionState
{
    public required TemplateDocument Document { get; init; }
    public string? FilePath { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsDirty { get; init; }
    public Guid CurrentRevision { get; init; }
    public Guid SavedRevision { get; init; }
    public IReadOnlySet<Guid> SelectedElementIds { get; init; } = new HashSet<Guid>();
    public IReadOnlyDictionary<Guid, ReadOnlyMemory<byte>> AssetContents { get; init; } =
        new Dictionary<Guid, ReadOnlyMemory<byte>>();
    public IReadOnlyList<ValidationIssue> ValidationIssues { get; init; } = Array.Empty<ValidationIssue>();
}

public sealed class DocumentSessionChangedEventArgs(DocumentSessionState state) : EventArgs
{
    public DocumentSessionState State { get; } = state;
}
