namespace Rmpp.Infrastructure.Persistence;

public sealed record DatabaseLocation(
    DatabaseStorageMode StorageMode,
    string DataDirectory,
    string DatabasePath,
    string MigrationLockPath,
    string PreMigrationBackupPath);

/// <summary>解析安装版与便携版数据目录，并保证便携模式不会回落到用户目录。</summary>
public class DatabaseLocationResolver
{
    public virtual DatabaseLocation Resolve(DatabaseOptions? options = null)
    {
        DatabaseOptions validated = (options ?? new DatabaseOptions()).Validate();
        string executableDirectory = Path.GetFullPath(validated.ExecutableDirectory ?? AppContext.BaseDirectory);
        DatabaseStorageMode mode = ResolveMode(validated, executableDirectory);
        string dataDirectory = mode == DatabaseStorageMode.Portable
            ? Path.Combine(executableDirectory, validated.PortableDataDirectoryName)
            : Path.Combine(
                Path.GetFullPath(validated.InstalledDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                validated.ApplicationDirectoryName);
        dataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(dataDirectory);

        string databasePath = Path.Combine(dataDirectory, validated.DatabaseFileName);
        return new DatabaseLocation(
            mode,
            dataDirectory,
            databasePath,
            databasePath + ".migration.lock",
            databasePath + ".pre-migration.bak");
    }

    private static DatabaseStorageMode ResolveMode(DatabaseOptions options, string executableDirectory)
    {
        if (options.StorageMode != DatabaseStorageMode.Auto)
        {
            return options.StorageMode;
        }

        return File.Exists(Path.Combine(executableDirectory, options.PortableFlagFileName))
            ? DatabaseStorageMode.Portable
            : DatabaseStorageMode.Installed;
    }
}
