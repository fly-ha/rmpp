namespace Rmpp.Infrastructure.Persistence.Models;

/// <summary>表示一个带类型和模式版本的本地设置记录。</summary>
public sealed record AppSettingRecord(
    string Key,
    string ValueJson,
    string ValueType,
    int SchemaVersion,
    DateTimeOffset UpdatedAt);

public sealed record SettingDefinition<T>(
    string Key,
    T DefaultValue,
    int SchemaVersion,
    Func<T, bool>? Validator = null);

public sealed record SettingReadResult<T>(T Value, bool UsedDefault, string? Issue = null);
