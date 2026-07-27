using Rmpp.Application.Data;

namespace Rmpp.Application.Abstractions;

/// <summary>隔离 CSV、Excel 等完全离线的数据源读取器。</summary>
public interface IDataSourceReader
{
    bool CanRead(string fileExtension);

    Task<DataSetSnapshot> ReadAsync(
        DataImportOptions options,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<DataRowSnapshot> PreviewAsync(
        DataImportOptions options,
        CancellationToken cancellationToken = default);
}
