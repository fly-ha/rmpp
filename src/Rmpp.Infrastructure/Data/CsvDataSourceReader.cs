using System.Runtime.CompilerServices;
using System.Text;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;

namespace Rmpp.Infrastructure.Data;

/// <summary>以流式字符解析完全离线读取 CSV，支持显式编码、分隔符、引号和资源上限。</summary>
public sealed class CsvDataSourceReader : IDataSourceReader
{
    static CsvDataSourceReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public bool CanRead(string fileExtension) =>
        StringComparer.OrdinalIgnoreCase.Equals(NormalizeExtension(fileExtension), ".csv")
        || StringComparer.OrdinalIgnoreCase.Equals(NormalizeExtension(fileExtension), ".txt");

    public async Task<DataSetSnapshot> ReadAsync(
        DataImportOptions options,
        CancellationToken cancellationToken = default)
    {
        DataImportOptions validated = options.Validate();
        IReadOnlyList<IReadOnlyList<string?>> records = await ReadRecordsAsync(
            validated,
            validated.MaximumRows + (validated.HasHeaderRow ? 1 : 0) + 1,
            cancellationToken).ConfigureAwait(false);
        int acceptedRecordCount = validated.MaximumRows + (validated.HasHeaderRow ? 1 : 0);
        bool truncated = records.Count > acceptedRecordCount;
        IReadOnlyList<IReadOnlyList<string?>> accepted = truncated
            ? records.Take(acceptedRecordCount).ToArray()
            : records;
        return DataImportSnapshotBuilder.Build(
            Path.GetFileName(validated.FilePath),
            accepted,
            validated,
            Array.Empty<string>(),
            truncated);
    }

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

    private static async Task<IReadOnlyList<IReadOnlyList<string?>>> ReadRecordsAsync(
        DataImportOptions options,
        int maximumRecords,
        CancellationToken cancellationToken)
    {
        Encoding encoding;
        try
        {
            encoding = Encoding.GetEncoding(
                options.EncodingName,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
        catch (ArgumentException exception)
        {
            throw new DataImportException("Selected text encoding is not available.", innerException: exception);
        }

        try
        {
            await using FileStream stream = new(
                options.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using StreamReader reader = new(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: false);
            CsvCharacterReader characters = new(reader);
            List<IReadOnlyList<string?>> records = [];
            int rowNumber = 1;
            while (records.Count < maximumRecords)
            {
                IReadOnlyList<string?>? record = await ReadRecordAsync(characters, options, rowNumber, cancellationToken).ConfigureAwait(false);
                if (record is null)
                {
                    break;
                }

                records.Add(record);
                rowNumber++;
            }

            return records;
        }
        catch (DecoderFallbackException exception)
        {
            throw new DataImportException("CSV contains bytes invalid for the selected encoding.", innerException: exception);
        }
        catch (IOException exception)
        {
            throw new DataImportException("CSV could not be read from local storage.", innerException: exception);
        }
    }

    /// <summary>解析一个 RFC 4180 风格记录，允许引号字段跨物理行但不允许未闭合引号。</summary>
    private static async Task<IReadOnlyList<string?>?> ReadRecordAsync(
        CsvCharacterReader reader,
        DataImportOptions options,
        int rowNumber,
        CancellationToken cancellationToken)
    {
        List<string?> fields = [];
        StringBuilder field = new();
        bool inQuotes = false;
        bool sawAnyCharacter = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int value = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (value < 0)
            {
                if (inQuotes)
                {
                    throw new DataImportException("CSV quoted field is not closed.", rowNumber, fields.Count + 1);
                }

                if (!sawAnyCharacter && fields.Count == 0 && field.Length == 0)
                {
                    return null;
                }

                AddField(fields, field, options, rowNumber);
                return fields;
            }

            char character = (char)value;
            sawAnyCharacter = true;
            if (inQuotes)
            {
                if (character == options.Quote)
                {
                    int next = await reader.PeekAsync(cancellationToken).ConfigureAwait(false);
                    if (next == options.Quote)
                    {
                        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                        field.Append(options.Quote);
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
            }
            else if (character == options.Delimiter)
            {
                AddField(fields, field, options, rowNumber);
            }
            else if (character == options.Quote && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && await reader.PeekAsync(cancellationToken).ConfigureAwait(false) == '\n')
                {
                    await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                }

                AddField(fields, field, options, rowNumber);
                return fields;
            }
            else
            {
                field.Append(character);
            }

            if (field.Length > options.MaximumFieldCharacters)
            {
                throw new DataImportException(
                    $"CSV field exceeds the configured {options.MaximumFieldCharacters} character limit.",
                    rowNumber,
                    fields.Count + 1);
            }
        }
    }

    private static void AddField(
        List<string?> fields,
        StringBuilder field,
        DataImportOptions options,
        int rowNumber)
    {
        if (fields.Count >= options.MaximumColumns)
        {
            throw new DataImportException(
                $"CSV row exceeds the configured {options.MaximumColumns} column limit.",
                rowNumber,
                fields.Count + 1);
        }

        fields.Add(field.ToString());
        field.Clear();
    }

    private static string NormalizeExtension(string value) =>
        value.StartsWith('.') ? value : Path.GetExtension(value);

    private sealed class CsvCharacterReader(StreamReader reader)
    {
        private readonly char[] buffer = new char[4096];
        private int offset;
        private int count;

        public async ValueTask<int> ReadAsync(CancellationToken cancellationToken)
        {
            if (offset >= count && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return -1;
            }

            return buffer[offset++];
        }

        public async ValueTask<int> PeekAsync(CancellationToken cancellationToken)
        {
            if (offset >= count && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return -1;
            }

            return buffer[offset];
        }

        private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
        {
            count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            offset = 0;
            return count > 0;
        }
    }
}
