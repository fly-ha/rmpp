namespace Rmpp.Application.Abstractions;

public enum PdfPixelFormat
{
    Bgra32,
}

/// <summary>保存 PDFium 产生的无压缩像素，不携带脚本、链接或其他活动内容。</summary>
public sealed record PdfRasterizedPage
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Stride { get; init; }
    public PdfPixelFormat PixelFormat { get; init; } = PdfPixelFormat.Bgra32;
    public required ReadOnlyMemory<byte> Pixels { get; init; }
}
