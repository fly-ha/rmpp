namespace Rmpp.Application.Abstractions;

/// <summary>定义只读 PDF 数据、从 1 开始的页码和有界输出像素尺寸。</summary>
public sealed record PdfRasterizationRequest
{
    public required ReadOnlyMemory<byte> DocumentBytes { get; init; }
    public int PageNumber { get; init; } = 1;
    public int TargetWidthPixels { get; init; }
    public int TargetHeightPixels { get; init; }
}
