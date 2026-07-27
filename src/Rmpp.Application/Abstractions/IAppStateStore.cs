namespace Rmpp.Application.Abstractions;

/// <summary>提供命名、版本化的本地状态读写，不规定 SQLite 或文件实现。</summary>
public interface IAppStateStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
