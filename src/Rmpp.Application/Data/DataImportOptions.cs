namespace Rmpp.Application.Data;

/// <summary>定义 CSV/XLSX 导入的显式解析选项和防御性资源上限。</summary>
public sealed record DataImportOptions
{
    public required string FilePath { get; init; }
    public bool HasHeaderRow { get; init; } = true;
    public string EncodingName { get; init; } = "utf-8";
    public char Delimiter { get; init; } = ',';
    public char Quote { get; init; } = '"';
    public string CultureName { get; init; } = string.Empty;
    public int MaximumRows { get; init; } = 100_000;
    public int MaximumColumns { get; init; } = 512;
    public int MaximumFieldCharacters { get; init; } = 1_000_000;
    public int PreviewRowLimit { get; init; } = 200;
    public string? WorksheetName { get; init; }
    public string? CellRange { get; init; }

    public DataImportOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(EncodingName);
        if (Delimiter == Quote || Delimiter is '\r' or '\n' || Quote is '\r' or '\n')
        {
            throw new ArgumentException("Delimiter and quote must be distinct single-line characters.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumRows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumColumns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumFieldCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PreviewRowLimit);
        return this;
    }
}
