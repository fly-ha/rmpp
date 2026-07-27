using Rmpp.Infrastructure.Persistence;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Persistence;

public sealed class DatabaseRecoveryTests
{
    [Fact]
    public async Task CorruptReplaceableDatabaseIsPreservedAndRecreated()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        Directory.CreateDirectory(database.Location.DataDirectory);
        byte[] corruptBytes = "not a sqlite database"u8.ToArray();
        await File.WriteAllBytesAsync(database.Location.DatabasePath, corruptBytes);

        await database.InitializeAsync();

        Assert.NotNull(database.LastCorruptDatabasePath);
        Assert.Equal(corruptBytes, await File.ReadAllBytesAsync(database.LastCorruptDatabasePath));
        Assert.True(File.Exists(database.Location.DatabasePath));
    }
}
