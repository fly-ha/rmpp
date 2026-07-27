using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence.Migrations;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>提供应用SQLite数据库的确定位置、连接初始化、迁移和统一重试入口。</summary>
public sealed class SqliteAppDatabase
{
    private readonly DatabaseMigrator migrator;
    private readonly DatabaseOptions options;
    private readonly SqliteRetryPolicy retryPolicy;

    public SqliteAppDatabase(
        DatabaseOptions? options = null,
        DatabaseLocationResolver? locationResolver = null,
        IEnumerable<IDatabaseMigration>? migrations = null)
    {
        this.options = (options ?? new DatabaseOptions()).Validate();
        Location = (locationResolver ?? new DatabaseLocationResolver()).Resolve(this.options);
        retryPolicy = new SqliteRetryPolicy(this.options);
        migrator = new DatabaseMigrator(this.options, migrations);
    }

    public DatabaseLocation Location { get; }
    public string? LastCorruptDatabasePath { get; private set; }

    public async Task<DatabaseMigrationResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await InitializeCoreAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 11 or 26)
        {
            SqliteConnection.ClearAllPools();
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
            string corruptPath = Location.DatabasePath + $".corrupt.{timestamp}";
            if (File.Exists(Location.DatabasePath))
            {
                File.Move(Location.DatabasePath, corruptPath);
            }

            MoveSidecarIfPresent(Location.DatabasePath + "-wal", corruptPath + "-wal");
            MoveSidecarIfPresent(Location.DatabasePath + "-shm", corruptPath + "-shm");
            LastCorruptDatabasePath = corruptPath;
            return await InitializeCoreAsync(cancellationToken);
        }
    }

    private async Task<DatabaseMigrationResult> InitializeCoreAsync(CancellationToken cancellationToken)
    {
        bool databaseExisted = File.Exists(Location.DatabasePath) && new FileInfo(Location.DatabasePath).Length > 0;
        await using SqliteConnection connection = await OpenRawConnectionAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, includeJournalMode: false, cancellationToken);
        DatabaseMigrationResult result = await retryPolicy.ExecuteAsync(
            token => migrator.MigrateAsync(connection, Location, databaseExisted, token),
            cancellationToken);
        await ConfigureConnectionAsync(connection, includeJournalMode: true, cancellationToken);
        return result;
    }

    private static void MoveSidecarIfPresent(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Move(source, destination);
        }
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        SqliteConnection connection = await OpenRawConnectionAsync(cancellationToken);
        try
        {
            await ConfigureConnectionAsync(connection, includeJournalMode: true, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        retryPolicy.ExecuteAsync(operation, cancellationToken);

    public Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        retryPolicy.ExecuteAsync(operation, cancellationToken);

    private async Task<SqliteConnection> OpenRawConnectionAsync(CancellationToken cancellationToken)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = Location.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = checked((int)Math.Ceiling(options.BusyTimeout.TotalSeconds)),
        };
        SqliteConnection connection = new(builder.ConnectionString);
        try
        {
            await retryPolicy.ExecuteAsync(token => connection.OpenAsync(token), cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task ConfigureConnectionAsync(
        SqliteConnection connection,
        bool includeJournalMode,
        CancellationToken cancellationToken)
    {
        await retryPolicy.ExecuteAsync(async token =>
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = includeJournalMode
                ? $"PRAGMA foreign_keys = ON; PRAGMA busy_timeout = {(long)options.BusyTimeout.TotalMilliseconds}; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;"
                : $"PRAGMA foreign_keys = ON; PRAGMA busy_timeout = {(long)options.BusyTimeout.TotalMilliseconds};";
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }
}
