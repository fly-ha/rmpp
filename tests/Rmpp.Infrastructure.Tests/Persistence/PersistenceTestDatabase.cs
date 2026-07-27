using Rmpp.Infrastructure.Persistence;

namespace Rmpp.Infrastructure.Tests.Persistence;

internal static class PersistenceTestDatabase
{
    public static SqliteAppDatabase Create(string directory, TimeSpan? busyTimeout = null) =>
        new(new DatabaseOptions
        {
            StorageMode = DatabaseStorageMode.Portable,
            ExecutableDirectory = directory,
            BusyTimeout = busyTimeout ?? TimeSpan.FromSeconds(2),
            MaximumRetryAttempts = 8,
            InitialRetryDelay = TimeSpan.FromMilliseconds(20),
            MigrationLockTimeout = TimeSpan.FromSeconds(5),
        });
}
