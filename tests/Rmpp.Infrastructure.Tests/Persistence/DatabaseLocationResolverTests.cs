using Rmpp.Infrastructure.Persistence;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Persistence;

public sealed class DatabaseLocationResolverTests
{
    [Fact]
    public async Task PortableFlagKeepsDataBesideApplication()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "portable.flag"), string.Empty);
        string installedRoot = Path.Combine(directory.Path, "installed-root");

        DatabaseLocation location = new DatabaseLocationResolver().Resolve(new DatabaseOptions
        {
            StorageMode = DatabaseStorageMode.Auto,
            ExecutableDirectory = directory.Path,
            InstalledDataRoot = installedRoot,
        });

        Assert.Equal(DatabaseStorageMode.Portable, location.StorageMode);
        Assert.StartsWith(Path.Combine(directory.Path, "data"), location.DatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(installedRoot));
    }

    [Fact]
    public async Task InstalledModeUsesConfiguredLocalDataRoot()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        string executable = Path.Combine(directory.Path, "app");
        string installedRoot = Path.Combine(directory.Path, "local-app-data");
        Directory.CreateDirectory(executable);

        DatabaseLocation location = new DatabaseLocationResolver().Resolve(new DatabaseOptions
        {
            StorageMode = DatabaseStorageMode.Installed,
            ExecutableDirectory = executable,
            InstalledDataRoot = installedRoot,
        });

        Assert.Equal(DatabaseStorageMode.Installed, location.StorageMode);
        Assert.StartsWith(Path.Combine(installedRoot, "RMPP Studio"), location.DatabasePath, StringComparison.OrdinalIgnoreCase);
    }
}
