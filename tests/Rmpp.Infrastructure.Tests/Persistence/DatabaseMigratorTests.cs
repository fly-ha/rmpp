using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Migrations;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Persistence;

public sealed class DatabaseMigratorTests
{
    [Fact]
    public async Task FreshDatabaseCreatesCurrentSchemaAndIsIdempotent()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = CreateDatabase(directory.Path);

        DatabaseMigrationResult first = await database.InitializeAsync();
        DatabaseMigrationResult second = await database.InitializeAsync();

        Assert.Equal(0, first.PreviousVersion);
        Assert.Equal(1, first.CurrentVersion);
        Assert.Equal(1, second.PreviousVersion);
        Assert.Equal(1, second.CurrentVersion);
        await using SqliteConnection connection = await database.OpenConnectionAsync();
        Assert.Equal(7, await CountApplicationTablesAsync(connection));
    }

    [Fact]
    public async Task FailedMigrationRollsBackAllSchemaChanges()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = CreateDatabase(
            directory.Path,
            [new V001CreateBaseSchema(), new FailingMigration()]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.InitializeAsync());

        await using SqliteConnection connection = await database.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'template_catalog';";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? -1L));
    }

    [Fact]
    public async Task ExistingDatabaseGetsPreMigrationBackup()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = CreateDatabase(directory.Path);
        await using (SqliteConnection connection = new($"Data Source={database.Location.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE legacy_marker(value TEXT NOT NULL); INSERT INTO legacy_marker VALUES ('keep');";
            await command.ExecuteNonQueryAsync();
        }

        DatabaseMigrationResult result = await database.InitializeAsync();

        Assert.Equal(database.Location.PreMigrationBackupPath, result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        await using SqliteConnection backup = new($"Data Source={result.BackupPath};Mode=ReadOnly");
        await backup.OpenAsync();
        await using SqliteCommand verify = backup.CreateCommand();
        verify.CommandText = "SELECT value FROM legacy_marker;";
        Assert.Equal("keep", await verify.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ConcurrentInitializationUsesSingleMigrationOwner()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase first = CreateDatabase(directory.Path);
        SqliteAppDatabase second = CreateDatabase(directory.Path);

        DatabaseMigrationResult[] results = await Task.WhenAll(first.InitializeAsync(), second.InitializeAsync());

        Assert.Contains(results, static result => result.PreviousVersion == 0 && result.CurrentVersion == 1);
        Assert.Contains(results, static result => result.PreviousVersion == 1 && result.CurrentVersion == 1);
    }

    private static SqliteAppDatabase CreateDatabase(
        string directory,
        IEnumerable<IDatabaseMigration>? migrations = null) =>
        new(
            new DatabaseOptions
            {
                StorageMode = DatabaseStorageMode.Portable,
                ExecutableDirectory = directory,
                BusyTimeout = TimeSpan.FromSeconds(2),
                MigrationLockTimeout = TimeSpan.FromSeconds(5),
            },
            migrations: migrations);

    private static async Task<long> CountApplicationTablesAsync(SqliteConnection connection)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                'template_catalog', 'template_tags', 'recent_files', 'calibration_profiles',
                'app_settings', 'recovery_sessions', 'schema_migrations');
            """;
        return (long)(await command.ExecuteScalarAsync() ?? -1L);
    }

    private sealed class FailingMigration : IDatabaseMigration
    {
        public int Version => 2;
        public string Name => "Always fail";

        public async Task ApplyAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            CancellationToken cancellationToken)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE should_rollback(id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(cancellationToken);
            throw new InvalidOperationException("Simulated migration failure.");
        }
    }
}
