using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>维护有界最近文件列表，仅保存路径和非敏感显示元数据。</summary>
public sealed class RecentFileRepository(SqliteAppDatabase database)
{
    /// <summary>删除指定类型和路径的最近文件记录，不删除实际文件。</summary>
    public async Task RemoveAsync(
        RecentFileKind kind,
        string path,
        CancellationToken cancellationToken = default)
    {
        string normalized = SqlitePersistenceHelpers.NormalizePath(path);
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM recent_files WHERE kind = $kind AND normalized_path = $path;";
            command.Parameters.AddWithValue("$kind", (int)kind);
            command.Parameters.AddWithValue("$path", normalized);
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    public async Task AddAsync(
        RecentFileEntry entry,
        int maximumEntriesPerKind = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntriesPerKind);

        string path = SqlitePersistenceHelpers.NormalizePath(entry.Path);
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            await using (SqliteCommand upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = """
                    INSERT INTO recent_files(kind, normalized_path, display_name, last_opened_at_utc, metadata_json)
                    VALUES ($kind, $path, $name, $opened, NULL)
                    ON CONFLICT(kind, normalized_path) DO UPDATE SET
                        display_name = excluded.display_name,
                        last_opened_at_utc = excluded.last_opened_at_utc,
                        metadata_json = NULL;
                    """;
                upsert.Parameters.AddWithValue("$kind", (int)entry.Kind);
                upsert.Parameters.AddWithValue("$path", path);
                upsert.Parameters.AddWithValue("$name", entry.DisplayName);
                upsert.Parameters.AddWithValue("$opened", SqlitePersistenceHelpers.FormatTimestamp(entry.LastOpenedAt));
                await upsert.ExecuteNonQueryAsync(token);
            }

            await using (SqliteCommand trim = connection.CreateCommand())
            {
                trim.Transaction = transaction;
                trim.CommandText = """
                    DELETE FROM recent_files
                    WHERE kind = $kind
                      AND normalized_path NOT IN (
                          SELECT normalized_path FROM recent_files
                          WHERE kind = $kind
                          ORDER BY last_opened_at_utc DESC
                          LIMIT $limit);
                    """;
                trim.Parameters.AddWithValue("$kind", (int)entry.Kind);
                trim.Parameters.AddWithValue("$limit", maximumEntriesPerKind);
                await trim.ExecuteNonQueryAsync(token);
            }

            await transaction.CommitAsync(token);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<RecentFileEntry>> GetAsync(
        RecentFileKind kind,
        CancellationToken cancellationToken = default)
    {
        return await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT normalized_path, display_name, last_opened_at_utc
                FROM recent_files WHERE kind = $kind
                ORDER BY last_opened_at_utc DESC;
                """;
            command.Parameters.AddWithValue("$kind", (int)kind);
            List<RecentFileEntry> result = [];
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                result.Add(new RecentFileEntry
                {
                    Kind = kind,
                    Path = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    LastOpenedAt = SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(2)),
                });
            }

            return (IReadOnlyList<RecentFileEntry>)result;
        }, cancellationToken);
    }
}
