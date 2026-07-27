namespace Rmpp.Application.Validation;

/// <summary>精确定位文档、页面、图层、元素、背景、资源或数据记录中的问题。</summary>
public sealed record ValidationLocation
{
    public Guid? DocumentId { get; init; }
    public int? PageNumber { get; init; }
    public Guid? LayerId { get; init; }
    public Guid? ElementId { get; init; }
    public Guid? BackgroundId { get; init; }
    public Guid? AssetId { get; init; }
    public int? RecordIndex { get; init; }
    public string? PropertyPath { get; init; }
}
