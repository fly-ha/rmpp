using System.Text.Json;
using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence.Models;

namespace Rmpp.Infrastructure.Persistence;

/// <summary>以带类型和版本的JSON保存设置，并在无效或过期时安全返回默认值。</summary>
public sealed class SettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
    private readonly SqliteAppDatabase database;

    public SettingsRepository(SqliteAppDatabase database)
    {
        this.database = database;
    }

    public async Task SetAsync<T>(
        SettingDefinition<T> definition,
        T value,
        CancellationToken cancellationToken = default)
    {
        ValidateDefinition(definition);
        if (definition.Validator is not null && !definition.Validator(value))
        {
            throw new ArgumentException($"Setting value is invalid: {definition.Key}", nameof(value));
        }

        string json = JsonSerializer.Serialize(value, JsonOptions);
        string valueType = typeof(T).FullName ?? typeof(T).Name;
        await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO app_settings(setting_key, value_json, value_type, schema_version, updated_at_utc)
                VALUES ($key, $json, $type, $version, $updated)
                ON CONFLICT(setting_key) DO UPDATE SET
                    value_json = excluded.value_json,
                    value_type = excluded.value_type,
                    schema_version = excluded.schema_version,
                    updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$key", definition.Key);
            command.Parameters.AddWithValue("$json", json);
            command.Parameters.AddWithValue("$type", valueType);
            command.Parameters.AddWithValue("$version", definition.SchemaVersion);
            command.Parameters.AddWithValue("$updated", SqlitePersistenceHelpers.FormatTimestamp(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(token);
        }, cancellationToken);
    }

    public async Task<SettingReadResult<T>> GetAsync<T>(
        SettingDefinition<T> definition,
        CancellationToken cancellationToken = default)
    {
        ValidateDefinition(definition);
        AppSettingRecord? record = await GetRecordAsync(definition.Key, cancellationToken);
        if (record is null)
        {
            return new SettingReadResult<T>(definition.DefaultValue, UsedDefault: true, "Setting is not stored.");
        }

        string expectedType = typeof(T).FullName ?? typeof(T).Name;
        if (record.SchemaVersion != definition.SchemaVersion || !string.Equals(record.ValueType, expectedType, StringComparison.Ordinal))
        {
            return new SettingReadResult<T>(definition.DefaultValue, UsedDefault: true, "Setting type or schema version is obsolete.");
        }

        try
        {
            T? value = JsonSerializer.Deserialize<T>(record.ValueJson, JsonOptions);
            if (value is null || definition.Validator is not null && !definition.Validator(value))
            {
                return new SettingReadResult<T>(definition.DefaultValue, UsedDefault: true, "Setting value failed validation.");
            }

            return new SettingReadResult<T>(value, UsedDefault: false);
        }
        catch (JsonException)
        {
            return new SettingReadResult<T>(definition.DefaultValue, UsedDefault: true, "Setting JSON is invalid.");
        }
    }

    private async Task<AppSettingRecord?> GetRecordAsync(string key, CancellationToken cancellationToken)
    {
        return await database.ExecuteWithRetryAsync(async token =>
        {
            await using SqliteConnection connection = await database.OpenConnectionAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT setting_key, value_json, value_type, schema_version, updated_at_utc
                FROM app_settings WHERE setting_key = $key;
                """;
            command.Parameters.AddWithValue("$key", key);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return null;
            }

            return new AppSettingRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                SqlitePersistenceHelpers.ParseTimestamp(reader.GetString(4)));
        }, cancellationToken);
    }

    private static void ValidateDefinition<T>(SettingDefinition<T> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Key) || definition.SchemaVersion <= 0)
        {
            throw new ArgumentException("Setting definition key and version are required.", nameof(definition));
        }
    }
}
