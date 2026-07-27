using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Rmpp.Infrastructure.Persistence.Migrations;

public sealed record DatabaseMigrationResult(int PreviousVersion, int CurrentVersion, string? BackupPath);

/// <summary>串行取得迁移所有权，创建迁移前备份，并在单个立即事务内执行连续迁移。</summary>
public sealed class DatabaseMigrator
{
    private readonly IDatabaseMigration[] migrations;
    private readonly DatabaseOptions options;

    public DatabaseMigrator(DatabaseOptions options, IEnumerable<IDatabaseMigration>? migrations = null)
    {
        this.options = options.Validate();
        this.migrations = (migrations ?? [new V001CreateBaseSchema()])
            .OrderBy(static migration => migration.Version)
            .ToArray();
        ValidateMigrationSequence(this.migrations);
    }

    public async Task<DatabaseMigrationResult> MigrateAsync(
        SqliteConnection connection,
        DatabaseLocation location,
        bool databaseExisted,
        CancellationToken cancellationToken = default)
    {
        await using FileStream migrationLock = await AcquireMigrationLockAsync(location.MigrationLockPath, cancellationToken);
        int previousVersion = await GetCurrentVersionAsync(connection, cancellationToken);
        int targetVersion = migrations.Length == 0 ? 0 : migrations[^1].Version;
        if (previousVersion > targetVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version {previousVersion} is newer than supported version {targetVersion}.");
        }

        IDatabaseMigration[] pending = migrations.Where(migration => migration.Version > previousVersion).ToArray();
        if (pending.Length == 0)
        {
            return new DatabaseMigrationResult(previousVersion, previousVersion, null);
        }

        string? backupPath = null;
        if (databaseExisted)
        {
            backupPath = location.PreMigrationBackupPath;
            SqliteConnectionStringBuilder backupBuilder = new()
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            };
            await using SqliteConnection backup = new(backupBuilder.ConnectionString);
            await backup.OpenAsync(cancellationToken);
            connection.BackupDatabase(backup);
        }

        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await EnsureMigrationTableAsync(connection, transaction, cancellationToken);
            int expectedVersion = previousVersion + 1;
            foreach (IDatabaseMigration migration in pending)
            {
                if (migration.Version != expectedVersion)
                {
                    throw new InvalidOperationException($"Missing database migration version {expectedVersion}.");
                }

                await migration.ApplyAsync(connection, transaction, cancellationToken);
                await RecordMigrationAsync(connection, transaction, migration, cancellationToken);
                expectedVersion++;
            }

            await transaction.CommitAsync(cancellationToken);
            return new DatabaseMigrationResult(previousVersion, targetVersion, backupPath);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<FileStream> AcquireMigrationLockAsync(string path, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + options.MigrationLockTimeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(options.InitialRetryDelay, cancellationToken);
            }
            catch (IOException exception)
            {
                throw new IOException("Timed out waiting for database migration ownership.", exception);
            }
        }
    }

    private static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations';";
        long tableCount = (long)(await exists.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (tableCount == 0)
        {
            return 0;
        }

        await using SqliteCommand version = connection.CreateCommand();
        version.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task EnsureMigrationTableAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RecordMigrationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IDatabaseMigration migration,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO schema_migrations(version, name, applied_at_utc)
            VALUES ($version, $name, $appliedAt);
            """;
        command.Parameters.AddWithValue("$version", migration.Version);
        command.Parameters.AddWithValue("$name", migration.Name);
        command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateMigrationSequence(IDatabaseMigration[] migrations)
    {
        for (int index = 0; index < migrations.Length; index++)
        {
            int expected = index + 1;
            if (migrations[index].Version != expected)
            {
                throw new ArgumentException($"Database migrations must be continuous from version 1; missing {expected}.");
            }
        }
    }
}
