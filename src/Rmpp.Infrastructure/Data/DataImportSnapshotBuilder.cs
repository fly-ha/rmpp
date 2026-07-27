using System.Globalization;
using Rmpp.Application.Data;
using Rmpp.Domain.Data;

namespace Rmpp.Infrastructure.Data;

/// <summary>把解析器产生的有序单元格转换为统一不可变快照。</summary>
internal static class DataImportSnapshotBuilder
{
    public static DataSetSnapshot Build(
        string sourceDisplayName,
        IReadOnlyList<IReadOnlyList<string?>> records,
        DataImportOptions options,
        IReadOnlyList<string> warnings,
        bool isTruncated)
    {
        if (records.Count == 0)
        {
            return new DataSetSnapshot
            {
                SourceDisplayName = sourceDisplayName,
                Schema = new DataSchema { Columns = Array.Empty<DataColumnDefinition>() },
                Rows = Array.Empty<DataRowSnapshot>(),
                Warnings = warnings,
                IsTruncated = isTruncated,
            };
        }

        int columnCount = records.Max(static record => record.Count);
        if (columnCount > options.MaximumColumns)
        {
            throw new DataImportException($"Data source exceeds the configured {options.MaximumColumns} column limit.");
        }

        IReadOnlyList<string?> header = options.HasHeaderRow ? records[0] : Array.Empty<string?>();
        int firstDataRow = options.HasHeaderRow ? 1 : 0;
        string[] names = CreateColumnNames(header, columnCount);
        List<DataRowSnapshot> rows = [];
        for (int rowIndex = firstDataRow; rowIndex < records.Count; rowIndex++)
        {
            IReadOnlyList<string?> record = records[rowIndex];
            Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
            for (int columnIndex = 0; columnIndex < names.Length; columnIndex++)
            {
                values[names[columnIndex]] = columnIndex < record.Count ? record[columnIndex] : null;
            }

            rows.Add(new DataRowSnapshot { Index = rows.Count, Values = values });
        }

        CultureInfo culture = string.IsNullOrWhiteSpace(options.CultureName)
            ? CultureInfo.InvariantCulture
            : CultureInfo.GetCultureInfo(options.CultureName);
        DataColumnDefinition[] columns = names.Select((name, ordinal) => new DataColumnDefinition(
            name,
            ordinal,
            InferType(rows.Select(row => row.GetValue(name)), culture),
            options.HasHeaderRow && ordinal < header.Count ? header[ordinal] : name)).ToArray();
        return new DataSetSnapshot
        {
            SourceDisplayName = sourceDisplayName,
            Schema = new DataSchema { Columns = columns },
            Rows = rows,
            Warnings = warnings,
            IsTruncated = isTruncated,
        };
    }

    private static string[] CreateColumnNames(IReadOnlyList<string?> header, int columnCount)
    {
        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        string[] names = new string[columnCount];
        for (int index = 0; index < columnCount; index++)
        {
            string baseName = index < header.Count && !string.IsNullOrWhiteSpace(header[index])
                ? header[index]!.Trim()
                : $"Column{index + 1}";
            string name = baseName;
            int suffix = 2;
            while (!used.Add(name))
            {
                name = $"{baseName}_{suffix++}";
            }

            names[index] = name;
        }

        return names;
    }

    private static FieldDataType InferType(IEnumerable<string?> values, CultureInfo culture)
    {
        string[] populated = values.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray()!;
        if (populated.Length == 0)
        {
            return FieldDataType.Text;
        }

        if (populated.All(value => bool.TryParse(value, out _)))
        {
            return FieldDataType.Boolean;
        }

        if (populated.All(value => decimal.TryParse(value, NumberStyles.Number, culture, out _)))
        {
            return FieldDataType.Number;
        }

        if (populated.All(value => DateTimeOffset.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out _)))
        {
            return FieldDataType.DateTime;
        }

        return FieldDataType.Text;
    }
}
