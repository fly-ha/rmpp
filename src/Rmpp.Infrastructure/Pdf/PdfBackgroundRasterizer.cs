using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Rmpp.Application.Abstractions;

namespace Rmpp.Infrastructure.Pdf;

/// <summary>使用本地 PDFium 将指定页面转为 BGRA 像素；不执行或暴露 PDF 活动内容。</summary>
public sealed class PdfBackgroundRasterizer(PdfRasterizationLimits? limits = null) : IPdfPageRasterizer
{
    private static readonly SemaphoreSlim PdfiumGate = new(1, 1);
    private readonly PdfRasterizationLimits limits = limits ?? new PdfRasterizationLimits();

    public async Task<PdfRasterizedPage> RasterizeAsync(
        PdfRasterizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        await PdfiumGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => Rasterize(request, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PdfiumGate.Release();
        }
    }

    private PdfRasterizedPage Rasterize(PdfRasterizationRequest request, CancellationToken cancellationToken)
    {
        byte[] document = request.DocumentBytes.ToArray();
        using IDocReader reader = DocLib.Instance.GetDocReader(
            document,
            new PageDimensions(request.TargetWidthPixels, request.TargetHeightPixels));
        int pageCount = reader.GetPageCount();
        if (pageCount <= 0 || pageCount > limits.MaximumPages)
        {
            throw new InvalidDataException("PDF page count exceeds configured safety limits.");
        }

        if (request.PageNumber > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Requested PDF page does not exist.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using IPageReader page = reader.GetPageReader(request.PageNumber - 1);
        int width = page.GetPageWidth();
        int height = page.GetPageHeight();
        ValidateOutput(width, height);
        byte[] pixels = page.GetImage();
        int stride = checked(width * 4);
        if (pixels.Length != checked(stride * height))
        {
            throw new InvalidDataException("PDFium returned an unexpected pixel buffer size.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new PdfRasterizedPage
        {
            Width = width,
            Height = height,
            Stride = stride,
            Pixels = pixels,
        };
    }

    private void ValidateRequest(PdfRasterizationRequest request)
    {
        if (request.DocumentBytes.IsEmpty || request.DocumentBytes.Length > limits.MaximumDocumentBytes)
        {
            throw new InvalidDataException("PDF byte length is outside configured safety limits.");
        }

        ReadOnlySpan<byte> header = request.DocumentBytes.Span;
        if (header.Length < 5 || !header[..5].SequenceEqual("%PDF-"u8))
        {
            throw new InvalidDataException("Input is not a PDF document.");
        }

        if (request.PageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "PDF page numbers start at one.");
        }

        ValidateOutput(request.TargetWidthPixels, request.TargetHeightPixels);
    }

    private void ValidateOutput(int width, int height)
    {
        long pixels = checked((long)width * height);
        if (width <= 0 || height <= 0
            || width > limits.MaximumDimensionPixels
            || height > limits.MaximumDimensionPixels
            || pixels > limits.MaximumPixels)
        {
            throw new InvalidDataException("PDF raster dimensions exceed configured safety limits.");
        }
    }
}
