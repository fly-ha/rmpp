using System.Globalization;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;

namespace Rmpp.Infrastructure.Data;

/// <summary>只读提取 XLSX/XLSM 缓存值，不执行公式、VBA、外部链接或嵌入对象。</summary>
public sealed class ExcelDataSourceReader : IDataSourceReader
{
    public bool CanRead(string fileExtension)
    {
        string extension = fileExtension.StartsWith('.')
            ? fileExtension
            : Path.GetExtension(fileExtension);
        return StringComparer.OrdinalIgnoreCase.Equals(extension, ".xlsx")
            || StringComparer.OrdinalIgnoreCase.Equals(extension, ".xlsm");
    }

    public Task<DataSetSnapshot> ReadAsync(
        DataImportOptions options,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadCore(options.Validate(), cancellationToken), cancellationToken);

    public async IAsyncEnumerable<DataRowSnapshot> PreviewAsync(
        DataImportOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DataImportOptions previewOptions = options.Validate() with
        {
            MaximumRows = Math.Min(options.MaximumRows, options.PreviewRowLimit),
        };
        DataSetSnapshot snapshot = await ReadAsync(previewOptions, cancellationToken).ConfigureAwait(false);
        foreach (DataRowSnapshot row in snapshot.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private static DataSetSnapshot ReadCore(DataImportOptions options, CancellationToken cancellationToken)
    {
        try
        {
            using SpreadsheetDocument spreadsheet = SpreadsheetDocument.Open(
                options.FilePath,
                false,
                new OpenSettings { AutoSave = false });
            WorkbookPart workbookPart = spreadsheet.WorkbookPart
                ?? throw new DataImportException("Workbook does not contain a workbook part.");
            Workbook workbook = workbookPart.Workbook
                ?? throw new DataImportException("Workbook definition is missing.");
            Sheet[] sheets = workbook.Sheets?.Elements<Sheet>().ToArray()
                ?? Array.Empty<Sheet>();
            Sheet sheet = SelectSheet(sheets, options.WorksheetName);
            WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(
                sheet.Id?.Value ?? throw new DataImportException("Worksheet relationship is missing."));
            List<string> warnings = ActiveContentWarnings(workbookPart, spreadsheet, options.FilePath);
            SharedStringTable? sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            Stylesheet? stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
            CellRangeBounds? range = CellRangeBounds.Parse(options.CellRange);
            int recordLimit = options.MaximumRows + (options.HasHeaderRow ? 1 : 0) + 1;
            List<IReadOnlyList<string?>> records = [];
            bool formulaWarningAdded = false;
            Worksheet worksheet = worksheetPart.Worksheet
                ?? throw new DataImportException("Selected worksheet content is missing.");
            foreach (Row row in worksheet.Descendants<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rowNumber = checked((int)(row.RowIndex?.Value ?? 0));
                if (range is not null && !range.ContainsRow(rowNumber))
                {
                    continue;
                }

                Dictionary<int, string?> values = [];
                int fallbackColumn = range?.StartColumn ?? 1;
                foreach (Cell cell in row.Elements<Cell>())
                {
                    int column = CellReferenceParser.Column(cell.CellReference?.Value) ?? fallbackColumn;
                    fallbackColumn = column + 1;
                    if (range is not null && !range.ContainsColumn(column))
                    {
                        continue;
                    }

                    if (cell.CellFormula is not null && !formulaWarningAdded)
                    {
                        warnings.Add("Workbook contains formulas; RMPP used cached values and did not calculate them.");
                        formulaWarningAdded = true;
                    }

                    string? value = ReadCellValue(cell, sharedStrings, stylesheet, options);
                    if (value is not null && value.Length > options.MaximumFieldCharacters)
                    {
                        throw new DataImportException(
                            $"Excel cell exceeds the configured {options.MaximumFieldCharacters} character limit.",
                            rowNumber,
                            column);
                    }

                    values[column] = value;
                }

                if (values.Count == 0 && range is null)
                {
                    continue;
                }

                int firstColumn = range?.StartColumn ?? (values.Count == 0 ? 1 : values.Keys.Min());
                int lastColumn = range?.EndColumn ?? (values.Count == 0 ? firstColumn : values.Keys.Max());
                int count = checked(lastColumn - firstColumn + 1);
                if (count > options.MaximumColumns)
                {
                    throw new DataImportException(
                        $"Worksheet selection exceeds the configured {options.MaximumColumns} column limit.",
                        rowNumber);
                }

                string?[] record = new string?[count];
                foreach ((int column, string? value) in values)
                {
                    record[column - firstColumn] = value;
                }

                records.Add(record);
                if (records.Count >= recordLimit)
                {
                    break;
                }
            }

            int acceptedRecordCount = options.MaximumRows + (options.HasHeaderRow ? 1 : 0);
            bool truncated = records.Count > acceptedRecordCount;
            IReadOnlyList<IReadOnlyList<string?>> accepted = truncated
                ? records.Take(acceptedRecordCount).ToArray()
                : records;
            return DataImportSnapshotBuilder.Build(
                $"{Path.GetFileName(options.FilePath)} / {sheet.Name?.Value}",
                accepted,
                options,
                warnings,
                truncated);
        }
        catch (DataImportException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FileFormatException or OpenXmlPackageException)
        {
            throw new DataImportException("Excel workbook is malformed, unsupported, or unavailable.", innerException: exception);
        }
    }

    private static Sheet SelectSheet(Sheet[] sheets, string? worksheetName)
    {
        if (sheets.Length == 0)
        {
            throw new DataImportException("Workbook does not contain any worksheets.");
        }

        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            return sheets[0];
        }

        return sheets.FirstOrDefault(sheet => StringComparer.OrdinalIgnoreCase.Equals(sheet.Name?.Value, worksheetName))
            ?? throw new DataImportException("Selected worksheet does not exist.");
    }

    private static List<string> ActiveContentWarnings(
        WorkbookPart workbookPart,
        SpreadsheetDocument document,
        string path)
    {
        List<string> warnings = [];
        if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(path), ".xlsm")
            || workbookPart.VbaProjectPart is not null)
        {
            warnings.Add("Workbook contains or may contain VBA; RMPP did not execute it.");
        }

        if (workbookPart.ExternalWorkbookParts.Any() || document.ExternalRelationships.Any())
        {
            warnings.Add("Workbook contains external links; RMPP did not open or refresh them.");
        }

        return warnings;
    }

    private static string? ReadCellValue(
        Cell cell,
        SharedStringTable? sharedStrings,
        Stylesheet? stylesheet,
        DataImportOptions options)
    {
        string? raw = cell.CellValue?.InnerText;
        CellValues? dataType = cell.DataType?.Value;
        if (dataType == CellValues.SharedString)
        {
            return ReadSharedString(raw, sharedStrings);
        }

        if (dataType == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText;
        }

        if (dataType == CellValues.Boolean)
        {
            return raw == "1" ? "true" : raw == "0" ? "false" : raw;
        }

        if (dataType == CellValues.String || dataType == CellValues.Error)
        {
            return raw;
        }

        return dataType == CellValues.Date
            ? FormatDate(raw, options.CultureName)
            : FormatNumericOrDate(raw, cell.StyleIndex?.Value, stylesheet, options.CultureName);
    }

    private static string? ReadSharedString(string? raw, SharedStringTable? sharedStrings)
    {
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
            || sharedStrings is null
            || index < 0
            || index >= sharedStrings.ChildElements.Count)
        {
            return raw;
        }

        return sharedStrings.ChildElements[index].InnerText;
    }

    private static string? FormatNumericOrDate(
        string? raw,
        uint? styleIndex,
        Stylesheet? stylesheet,
        string cultureName)
    {
        if (raw is null || styleIndex is null || stylesheet?.CellFormats is null)
        {
            return raw;
        }

        int index = checked((int)styleIndex.Value);
        if (index < 0 || index >= stylesheet.CellFormats.ChildElements.Count
            || stylesheet.CellFormats.ChildElements[index] is not CellFormat format
            || !IsDateFormat(format.NumberFormatId?.Value ?? 0, stylesheet))
        {
            return raw;
        }

        return FormatDate(raw, cultureName);
    }

    private static string? FormatDate(string? raw, string cultureName)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
        {
            return raw;
        }

        CultureInfo culture = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(cultureName);
        try
        {
            return DateTime.FromOADate(serial).ToString("G", culture);
        }
        catch (ArgumentException)
        {
            return raw;
        }
    }

    private static bool IsDateFormat(uint formatId, Stylesheet stylesheet)
    {
        if (formatId is >= 14 and <= 22 or >= 45 and <= 47)
        {
            return true;
        }

        NumberingFormat? custom = stylesheet.NumberingFormats?
            .Elements<NumberingFormat>()
            .FirstOrDefault(format => format.NumberFormatId?.Value == formatId);
        string code = custom?.FormatCode?.Value ?? string.Empty;
        string normalized = code.Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains('y')
            || normalized.Contains('d')
            || normalized.Contains("h:", StringComparison.Ordinal)
            || normalized.Contains("m:", StringComparison.Ordinal)
            || normalized.Contains('s');
    }

    private sealed record CellRangeBounds(int StartColumn, int StartRow, int EndColumn, int EndRow)
    {
        public bool ContainsRow(int row) => row >= StartRow && row <= EndRow;

        public bool ContainsColumn(int column) => column >= StartColumn && column <= EndColumn;

        public static CellRangeBounds? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2)
            {
                throw new DataImportException("Excel cell range is invalid.");
            }

            (int column, int row) start = CellReferenceParser.Parse(parts[0]);
            (int column, int row) end = parts.Length == 1 ? start : CellReferenceParser.Parse(parts[1]);
            return new CellRangeBounds(
                Math.Min(start.column, end.column),
                Math.Min(start.row, end.row),
                Math.Max(start.column, end.column),
                Math.Max(start.row, end.row));
        }
    }

    private static class CellReferenceParser
    {
        public static int? Column(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            int column = 0;
            foreach (char character in reference.TrimStart('$'))
            {
                if (!char.IsAsciiLetter(character))
                {
                    break;
                }

                column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
            }

            return column == 0 ? null : column;
        }

        public static (int Column, int Row) Parse(string reference)
        {
            string normalized = reference.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            int split = 0;
            while (split < normalized.Length && char.IsAsciiLetter(normalized[split]))
            {
                split++;
            }

            if (split == 0 || split == normalized.Length
                || Column(normalized[..split]) is not int column
                || !int.TryParse(normalized[split..], NumberStyles.None, CultureInfo.InvariantCulture, out int row)
                || row <= 0)
            {
                throw new DataImportException("Excel cell range is invalid.");
            }

            return (column, row);
        }
    }
}
