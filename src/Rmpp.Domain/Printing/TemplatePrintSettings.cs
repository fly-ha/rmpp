namespace Rmpp.Domain.Printing;

/// <summary>定义副本的页面排序方式，避免依赖打印驱动含义不一致的“逐份打印”选项。</summary>
public enum PrintCopyOrder
{
    /// <summary>同一逻辑记录的副本连续输出，例如 1、1、2、2、3、3。</summary>
    PerRecord,

    /// <summary>完整记录序列按批重复，例如 1、2、3、1、2、3。</summary>
    Collated,
}

/// <summary>保存在模板中的打印默认值；首选打印机只是可选偏好，不构成模板运行前提。</summary>
public sealed record TemplatePrintSettings
{
    public string? PreferredPrinterId { get; init; }
    public string? PreferredPrinterDisplayName { get; init; }
    public int OutputCount { get; init; } = 1;
    public bool UseAllRecords { get; init; } = true;
    public int FirstRecord { get; init; } = 1;
    public int LastRecord { get; init; } = 1;
    public int Copies { get; init; } = 1;
    public PrintCopyOrder CopyOrder { get; init; } = PrintCopyOrder.PerRecord;
    public int? StartingCell { get; init; }
    public bool IncludePrintableBackgrounds { get; init; } = true;

    public TemplatePrintSettings Validate()
    {
        if (PreferredPrinterId is not null && string.IsNullOrWhiteSpace(PreferredPrinterId))
        {
            throw new ArgumentException("首选打印机 ID 不能为空白。", nameof(PreferredPrinterId));
        }
        if (PreferredPrinterDisplayName is not null && string.IsNullOrWhiteSpace(PreferredPrinterDisplayName))
        {
            throw new ArgumentException("首选打印机名称不能为空白。", nameof(PreferredPrinterDisplayName));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OutputCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(FirstRecord);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(LastRecord);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Copies);
        if (LastRecord < FirstRecord)
        {
            throw new ArgumentOutOfRangeException(nameof(LastRecord), "末条记录不能小于首条记录。");
        }

        if (StartingCell is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StartingCell), "起始标签格必须大于零。");
        }

        return this;
    }
}
