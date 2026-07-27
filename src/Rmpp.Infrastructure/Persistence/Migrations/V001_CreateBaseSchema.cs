using Microsoft.Data.Sqlite;

namespace Rmpp.Infrastructure.Persistence.Migrations;

/// <summary>创建目录、标签、最近路径、校准、设置与恢复会话基础表。</summary>
public sealed class V001CreateBaseSchema : IDatabaseMigration
{
    public int Version => 1;
    public string Name => "Create base application-state schema";

    public async Task ApplyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE template_catalog (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id TEXT NOT NULL,
                normalized_path TEXT NOT NULL COLLATE NOCASE UNIQUE,
                title TEXT NOT NULL,
                description TEXT NOT NULL DEFAULT '',
                file_modified_at_utc TEXT NOT NULL,
                document_modified_at_utc TEXT NOT NULL,
                last_scanned_at_utc TEXT NOT NULL,
                status INTEGER NOT NULL,
                thumbnail_png BLOB NULL
            );
            CREATE INDEX ix_template_catalog_document_id ON template_catalog(document_id);

            CREATE TABLE template_tags (
                template_id INTEGER NOT NULL REFERENCES template_catalog(id) ON DELETE CASCADE,
                tag TEXT NOT NULL COLLATE NOCASE,
                PRIMARY KEY (template_id, tag)
            );

            CREATE TABLE recent_files (
                kind INTEGER NOT NULL,
                normalized_path TEXT NOT NULL COLLATE NOCASE,
                display_name TEXT NOT NULL,
                last_opened_at_utc TEXT NOT NULL,
                metadata_json TEXT NULL,
                PRIMARY KEY (kind, normalized_path)
            );
            CREATE INDEX ix_recent_files_last_opened ON recent_files(kind, last_opened_at_utc DESC);

            CREATE TABLE calibration_profiles (
                printer_stable_id TEXT NOT NULL,
                media_key TEXT NOT NULL,
                offset_x_mm REAL NOT NULL,
                offset_y_mm REAL NOT NULL,
                scale_x REAL NOT NULL,
                scale_y REAL NOT NULL,
                rotation_degrees REAL NOT NULL,
                last_verified_at_utc TEXT NULL,
                updated_at_utc TEXT NOT NULL,
                PRIMARY KEY (printer_stable_id, media_key)
            );

            CREATE TABLE app_settings (
                setting_key TEXT PRIMARY KEY,
                value_json TEXT NOT NULL,
                value_type TEXT NOT NULL,
                schema_version INTEGER NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE recovery_sessions (
                session_id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                original_template_path TEXT NULL,
                recovery_package_path TEXT NOT NULL COLLATE NOCASE,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                state INTEGER NOT NULL
            );
            CREATE INDEX ix_recovery_sessions_updated ON recovery_sessions(updated_at_utc DESC);
            """;

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
