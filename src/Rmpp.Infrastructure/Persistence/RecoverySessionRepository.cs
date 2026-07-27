using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>保存恢复包索引和生命周期状态，恢复正文始终位于独立 `.rmpp` 文件。</summary>
public sealed class RecoverySessionRepository(SqliteAppDatabase database)
{
    public async Task SaveAsync(RecoverySession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.SessionId == Guid.Empty || session.DocumentId == Guid.Empty)
        {
            throw new ArgumentException("Recovery session and document ids are required.", nameof(session));
        }

        string recoveryPath = SqlitePersistenceHelpers.NormalizePath(session.RecoveryPackagePath);
        string? originalPath = session.OriginalTemplatePath is null
            ? null
            : SqlitePersistenceHelpers.NormalizePath(session.OriginalTemplatePath);
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO recovery_sessions(
                    session_id, document_id, original_template_path, recovery_package_path,
                    created_at_utc, updated_at_utc, state)
                VALUES ($sessionId, $documentId, $original, $recovery, $created, $updated, $state)
                ON CONFLICT(session_id) DO UPDATE SET
                    document_id = excluded.document_id,
                    original_template_path = excluded.original_template_path,
                    recovery_package_path = excluded.recovery_package_path,
                    updated_at_utc = excluded.updated_at_utc,
                    state = excluded.state;
                """;
            command.Parameters.AddWithValue("$sessionId", session.SessionId.ToString("D"));
            command.Parameters.AddWithValue("$documentId", session.DocumentId.ToString("D"));
            command.Parameters.AddWithValue("$original", (object?)originalPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$recovery", recoveryPath);
            command.Parameters.AddWithValue("$created", SqlitePersistenceHelpers.FormatTimestamp(session.CreatedAt));
            command.Parameters.AddWithValue("$updated", SqlitePersistenceHelpers.FormatTimestamp(session.UpdatedAt));
            command.Parameters.AddWithValue("$state", (int)session.State);
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<RecoverySession>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT session_id, document_id, original_template_path, recovery_package_path,
                       created_at_utc, updated_at_utc, state
                FROM recovery_sessions
                WHERE state = $state
                ORDER BY updated_at_utc DESC;
                """;
            command.Parameters.AddWithValue("$state", (int)RecoverySessionState.Available);
            List<RecoverySession> sessions = [];
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                sessions.Add(new RecoverySession
                {
                    SessionId = Guid.Parse(reader.GetString(0)),
                    DocumentId = Guid.Parse(reader.GetString(1)),
                    OriginalTemplatePath = reader.IsDBNull(2) ? null : reader.GetString(2),
                    RecoveryPackagePath = reader.GetString(3),
                    CreatedAt = SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(4)),
                    UpdatedAt = SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(5)),
                    State = (RecoverySessionState)reader.GetInt32(6),
                });
            }

            return (IReadOnlyList<RecoverySession>)sessions;
        }, cancellationToken);
    }

    public async Task SetStateAsync(
        Guid sessionId,
        RecoverySessionState state,
        CancellationToken cancellationToken = default)
    {
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE recovery_sessions
                SET state = $state, updated_at_utc = $updated
                WHERE session_id = $sessionId;
                """;
            command.Parameters.AddWithValue("$state", (int)state);
            command.Parameters.AddWithValue("$updated", SqlitePersistenceHelpers.FormatTimestamp(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }
}
