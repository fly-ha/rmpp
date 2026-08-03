using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;

namespace Rmpp.Infrastructure.Pdf;

/// <summary>使用本地 PDFium 回读导出文件，确认页树与页面对象可被标准解析器打开。</summary>
public static class PdfDocumentVerifier
{
    private static readonly SemaphoreSlim PdfiumGate = new(1, 1);
    private const long MaximumPdfBytes = 512L * 1024 * 1024;

    public static async Task VerifyAsync(
        string filePath,
        int expectedPageCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedPageCount);
        FileInfo file = new(filePath);
        if (!file.Exists || file.Length < 8 || file.Length > MaximumPdfBytes)
        {
            throw new InvalidDataException("PDF 文件不存在、为空或超过本地验证限制。");
        }

        byte[] bytes = await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);
        await PdfiumGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() => Verify(bytes, expectedPageCount, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PdfiumGate.Release();
        }
    }

    private static void Verify(byte[] bytes, int expectedPageCount, CancellationToken cancellationToken)
    {
        try
        {
            using IDocReader reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(64, 64));
            int pageCount = reader.GetPageCount();
            if (pageCount != expectedPageCount)
            {
                throw new InvalidDataException($"PDF 页数验证失败：预期 {expectedPageCount} 页，实际 {pageCount} 页。");
            }

            for (int index = 0; index < pageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using IPageReader page = reader.GetPageReader(index);
                if (page.GetPageWidth() <= 0 || page.GetPageHeight() <= 0)
                {
                    throw new InvalidDataException($"PDF 第 {index + 1} 页尺寸无效。");
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not InvalidDataException)
        {
            throw new InvalidDataException("PDF 结构无法被本地解析器打开。", exception);
        }
    }
}
