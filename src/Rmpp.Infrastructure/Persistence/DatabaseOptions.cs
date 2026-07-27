namespace Rmpp.Infrastructure.Persistence;

public enum DatabaseStorageMode
{
    Auto,
    Installed,
    Portable,
}

/// <summary>定义本地应用数据库的位置、连接超时和有界重试策略。</summary>
public sealed record DatabaseOptions
{
    public DatabaseStorageMode StorageMode { get; init; } = DatabaseStorageMode.Auto;
    public string ApplicationDirectoryName { get; init; } = "RMPP Studio";
    public string DatabaseFileName { get; init; } = "rmpp.db";
    public string PortableDataDirectoryName { get; init; } = "data";
    public string PortableFlagFileName { get; init; } = "portable.flag";
    public string? ExecutableDirectory { get; init; }
    public string? InstalledDataRoot { get; init; }
    public TimeSpan BusyTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public int MaximumRetryAttempts { get; init; } = 6;
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(40);
    public TimeSpan MigrationLockTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public DatabaseOptions Validate()
    {
        if (string.IsNullOrWhiteSpace(ApplicationDirectoryName) ||
            string.IsNullOrWhiteSpace(DatabaseFileName) ||
            Path.GetFileName(DatabaseFileName) != DatabaseFileName ||
            string.IsNullOrWhiteSpace(PortableDataDirectoryName) ||
            Path.GetFileName(PortableDataDirectoryName) != PortableDataDirectoryName ||
            string.IsNullOrWhiteSpace(PortableFlagFileName) ||
            Path.GetFileName(PortableFlagFileName) != PortableFlagFileName)
        {
            throw new ArgumentException("Database path options contain an invalid name.");
        }

        if (BusyTimeout <= TimeSpan.Zero || MaximumRetryAttempts <= 0 ||
            InitialRetryDelay < TimeSpan.Zero || MigrationLockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(BusyTimeout), "Database timeout and retry values must be positive.");
        }

        return this;
    }
}
