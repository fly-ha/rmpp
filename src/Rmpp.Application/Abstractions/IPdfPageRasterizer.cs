namespace Rmpp.Application.Abstractions;

/// <summary>隔离 PDF 页面栅格化实现，使应用层不依赖 PDFium 或任何图形后端。</summary>
public interface IPdfPageRasterizer
{
    Task<PdfRasterizedPage> RasterizeAsync(
        PdfRasterizationRequest request,
        CancellationToken cancellationToken = default);
}
