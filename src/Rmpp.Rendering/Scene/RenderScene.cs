namespace Rmpp.Rendering.Scene;

/// <summary>供预览、PDF和打印后端共同消费的不可变有序物理场景。</summary>
public sealed record RenderScene
{
    public required Guid DocumentId { get; init; }
    public required IReadOnlyList<RenderPage> Pages { get; init; }
    public IReadOnlyList<RenderIssue> Issues { get; init; } = Array.Empty<RenderIssue>();
}
