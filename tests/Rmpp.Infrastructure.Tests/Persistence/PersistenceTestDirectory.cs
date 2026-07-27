using Microsoft.Data.Sqlite;

namespace Rmpp.Infrastructure.Tests.Persistence;

internal sealed class PersistenceTestDirectory : IAsyncDisposable
{
    private PersistenceTestDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static PersistenceTestDirectory Create()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "rmpp-tests",
            "persistence",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new PersistenceTestDirectory(path);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
