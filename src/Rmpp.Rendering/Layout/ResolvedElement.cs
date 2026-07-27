namespace Rmpp.Rendering.Layout;

/// <summary>保存数据、日期、流水号或变量图片解析后的会话值。</summary>
public sealed record ResolvedElement
{
    public required Guid ElementId { get; init; }
    public string? Text { get; init; }
    public Guid? AssetId { get; init; }
    public string? LocalImagePath { get; init; }
}
