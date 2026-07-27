namespace Rmpp.Application.Data;

/// <summary>提供按范围异步读取的会话内数据预览，不要求界面一次物化全部行。</summary>
public interface IDataPreviewSource
{
    int Count { get; }

    IAsyncEnumerable<DataRowSnapshot> ReadRangeAsync(
        int offset,
        int count,
        CancellationToken cancellationToken = default);
}
