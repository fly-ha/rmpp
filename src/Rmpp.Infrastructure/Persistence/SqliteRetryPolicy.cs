using Microsoft.Data.Sqlite;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>仅对SQLite临时忙碌或锁冲突执行有界、可取消的指数退避。</summary>
public sealed class SqliteRetryPolicy
{
    private readonly DatabaseOptions options;

    public SqliteRetryPolicy(DatabaseOptions options)
    {
        this.options = options.Validate();
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        _ = await ExecuteAsync(async token =>
        {
            await operation(token);
            return true;
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        TimeSpan delay = options.InitialRetryDelay;
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await operation(cancellationToken);
            }
            catch (SqliteException exception) when (IsTransient(exception) && attempt < options.MaximumRetryAttempts)
            {
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, options.BusyTimeout.TotalMilliseconds));
            }
        }
    }

    public static bool IsTransient(SqliteException exception) => exception.SqliteErrorCode is 5 or 6;
}
