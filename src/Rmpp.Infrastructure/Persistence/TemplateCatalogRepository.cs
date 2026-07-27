using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>维护可重建模板目录、标签、缺失状态和重复文档身份。</summary>
public sealed class TemplateCatalogRepository(SqliteAppDatabase database)
{
    public async Task UpsertAsync(TemplateCatalogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string path = SqlitePersistenceHelpers.NormalizePath(entry.Path);
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO template_catalog(
                        document_id, normalized_path, title, description, file_modified_at_utc,
                        document_modified_at_utc, last_scanned_at_utc, status, thumbnail_png)
                    VALUES ($documentId, $path, $title, $description, $fileModified, $documentModified, $scanned, $status, $thumbnail)
                    ON CONFLICT(normalized_path) DO UPDATE SET
                        document_id = excluded.document_id,
                        title = excluded.title,
                        description = excluded.description,
                        file_modified_at_utc = excluded.file_modified_at_utc,
                        document_modified_at_utc = excluded.document_modified_at_utc,
                        last_scanned_at_utc = excluded.last_scanned_at_utc,
                        status = excluded.status,
                        thumbnail_png = excluded.thumbnail_png;
                    """;
                command.Parameters.AddWithValue("$documentId", entry.DocumentId.ToString("D"));
                command.Parameters.AddWithValue("$path", path);
                command.Parameters.AddWithValue("$title", entry.Title);
                command.Parameters.AddWithValue("$description", entry.Description);
                command.Parameters.AddWithValue("$fileModified", SqlitePersistenceHelpers.FormatTimestamp(entry.FileModifiedAt));
                command.Parameters.AddWithValue("$documentModified", SqlitePersistenceHelpers.FormatTimestamp(entry.DocumentModifiedAt));
                command.Parameters.AddWithValue("$scanned", SqlitePersistenceHelpers.FormatTimestamp(entry.LastScannedAt));
                command.Parameters.AddWithValue("$status", (int)entry.Status);
                command.Parameters.AddWithValue("$thumbnail", (object?)entry.ThumbnailPng ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(token);
            }

            long templateId = await GetIdByPathAsync(connection, transaction, path, token);
            await ReplaceTagsAsync(connection, transaction, templateId, entry.Tags, token);
            await ReconcileDuplicateIdentityAsync(connection, transaction, entry.DocumentId, token);
            await transaction.CommitAsync(token);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TemplateCatalogEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, document_id, normalized_path, title, description, file_modified_at_utc,
                       document_modified_at_utc, last_scanned_at_utc, status, thumbnail_png
                FROM template_catalog
                ORDER BY title COLLATE NOCASE, normalized_path COLLATE NOCASE;
                """;
            List<TemplateCatalogEntry> entries = [];
            await using (SqliteDataReader reader = await command.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
                {
                    entries.Add(new TemplateCatalogEntry
                    {
                        Id = reader.GetInt64(0),
                        DocumentId = Guid.Parse(reader.GetString(1)),
                        Path = reader.GetString(2),
                        Title = reader.GetString(3),
                        Description = reader.GetString(4),
                        FileModifiedAt = SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(5)),
                        DocumentModifiedAt = SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(6)),
                        LastScannedAt = SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(7)),
                        Status = (TemplateCatalogStatus)reader.GetInt32(8),
                        ThumbnailPng = reader.IsDBNull(9) ? null : (byte[])reader[9],
                    });
                }
            }

            for (int index = 0; index < entries.Count; index++)
            {
                TemplateCatalogEntry entry = entries[index];
                entries[index] = entry with { Tags = await GetTagsAsync(connection, entry.Id, token) };
            }

            return (IReadOnlyList<TemplateCatalogEntry>)entries;
        }, cancellationToken);
    }

    public async Task ReconcileScannedRootsAsync(
        IEnumerable<string> roots,
        IReadOnlySet<string> existingPaths,
        CancellationToken cancellationToken = default)
    {
        string[] normalizedRoots = roots.Select(SqlitePersistenceHelpers.NormalizePath).ToArray();
        HashSet<string> normalizedExisting = existingPaths
            .Select(SqlitePersistenceHelpers.NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<TemplateCatalogEntry> entries = await GetAllAsync(cancellationToken);
        foreach (TemplateCatalogEntry entry in entries)
        {
            if (normalizedRoots.Any(root => IsUnderRoot(entry.Path, root)) && !normalizedExisting.Contains(entry.Path))
            {
                await SetStatusAsync(entry.Path, TemplateCatalogStatus.Missing, cancellationToken);
            }
        }
    }

    public async Task SetStatusAsync(
        string path,
        TemplateCatalogStatus status,
        CancellationToken cancellationToken = default)
    {
        string normalized = SqlitePersistenceHelpers.NormalizePath(path);
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            Guid? documentId = null;
            await using (SqliteCommand select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = "SELECT document_id FROM template_catalog WHERE normalized_path = $path;";
                select.Parameters.AddWithValue("$path", normalized);
                if (await select.ExecuteScalarAsync(token) is string value)
                {
                    documentId = Guid.Parse(value);
                }
            }

            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "UPDATE template_catalog SET status = $status WHERE normalized_path = $path;";
                command.Parameters.AddWithValue("$status", (int)status);
                command.Parameters.AddWithValue("$path", normalized);
                await command.ExecuteNonQueryAsync(token);
            }

            if (documentId is { } id)
            {
                await ReconcileDuplicateIdentityAsync(connection, transaction, id, token);
            }

            await transaction.CommitAsync(token);
        }, cancellationToken);
    }

    private static async Task<long> GetIdByPathAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string path,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM template_catalog WHERE normalized_path = $path;";
        command.Parameters.AddWithValue("$path", path);
        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Template catalogue upsert did not return an id."));
    }

    private static async Task ReplaceTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long templateId,
        IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        await using (SqliteCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM template_tags WHERE template_id = $id;";
            delete.Parameters.AddWithValue("$id", templateId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (string tag in tags.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO template_tags(template_id, tag) VALUES ($id, $tag);";
            insert.Parameters.AddWithValue("$id", templateId);
            insert.Parameters.AddWithValue("$tag", tag.Trim());
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<string>> GetTagsAsync(
        SqliteConnection connection,
        long templateId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT tag FROM template_tags WHERE template_id = $id ORDER BY tag COLLATE NOCASE;";
        command.Parameters.AddWithValue("$id", templateId);
        List<string> tags = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(reader.GetString(0));
        }

        return tags;
    }

    private static async Task ReconcileDuplicateIdentityAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE template_catalog
            SET status = CASE
                WHEN (SELECT COUNT(*) FROM template_catalog WHERE document_id = $documentId AND status <> $missing) > 1
                    THEN $duplicate
                ELSE $available
            END
            WHERE document_id = $documentId AND status <> $missing;
            """;
        command.Parameters.AddWithValue("$documentId", documentId.ToString("D"));
        command.Parameters.AddWithValue("$missing", (int)TemplateCatalogStatus.Missing);
        command.Parameters.AddWithValue("$duplicate", (int)TemplateCatalogStatus.DuplicateIdentity);
        command.Parameters.AddWithValue("$available", (int)TemplateCatalogStatus.Available);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool IsUnderRoot(string path, string root)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
