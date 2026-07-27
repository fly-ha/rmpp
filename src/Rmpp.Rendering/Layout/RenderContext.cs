namespace Rmpp.Rendering.Layout;

using Rmpp.Rendering.Scene;

public enum RenderTarget
{
    Editor,
    Preview,
    Pdf,
    Print,
}

/// <summary>定义一次不可变场景构建所用的目标、解析值和参考时间。</summary>
public sealed record RenderContext
{
    public RenderTarget Target { get; init; } = RenderTarget.Editor;
    public DateTimeOffset ReferenceTime { get; init; } = DateTimeOffset.UtcNow;
    public RenderTransform PlacementTransform { get; init; } = RenderTransform.Identity;
    public IReadOnlyDictionary<Guid, ResolvedElement> ResolvedElements { get; init; } =
        new Dictionary<Guid, ResolvedElement>();

    public ResolvedElement? Find(Guid elementId) =>
        ResolvedElements.TryGetValue(elementId, out ResolvedElement? value) ? value : null;

    public bool RequiresPrintable => Target is RenderTarget.Preview or RenderTarget.Pdf or RenderTarget.Print;
}
