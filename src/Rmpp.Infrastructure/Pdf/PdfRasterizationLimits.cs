namespace Rmpp.Infrastructure.Pdf;

/// <summary>限制输入 PDF、页数和输出像素，防止恶意文件造成资源耗尽。</summary>
public sealed record PdfRasterizationLimits
{
    public long MaximumDocumentBytes { get; init; } = 256L * 1024 * 1024;
    public int MaximumPages { get; init; } = 100;
    public int MaximumDimensionPixels { get; init; } = 16_384;
    public long MaximumPixels { get; init; } = 100_000_000;
}
