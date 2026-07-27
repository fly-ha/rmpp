using Rmpp.Desktop.Composition;
using Rmpp.Desktop.Services;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Templates;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Rmpp.Desktop.Tests.Services;

public sealed class Phase12LocalServicesTests
{
    [Fact]
    public void LocalHelpRejectsTraversalAndReadsOnlyPackagedTopics()
    {
        string root = Path.Combine(Path.GetTempPath(), "rmpp-help-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "start.md"), "# 本地帮助\n正文");
            LocalHelpService help = new(root);
            Assert.Equal("本地帮助", Assert.Single(help.GetTopics()).Title);
            Assert.Contains("正文", help.ReadTopic("start"));
            Assert.Throws<FileNotFoundException>(() => help.ReadTopic("../outside"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RecoveryStoresPackageOutsideSqliteAndSupportsRestoreAndDiscard()
    {
        string root = Path.Combine(Path.GetTempPath(), "rmpp-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SqliteAppDatabase database = new(new DatabaseOptions
            {
                StorageMode = DatabaseStorageMode.Installed,
                InstalledDataRoot = root,
                ApplicationDirectoryName = "data",
            });
            await database.InitializeAsync();
            AppStoragePaths paths = new(Path.Combine(root, "state"), false);
            Directory.CreateDirectory(paths.RecoveryDirectory);
            RecoveryCoordinator coordinator = new(paths, new RecoverySessionRepository(database), new AtomicTemplateFileWriter(), new RmppPackageReader());
            TemplatePackageContent content = new() { Document = TemplateDocument.CreateNew("恢复测试") };

            var session = await coordinator.SaveAsync(content, null);
            Assert.True(File.Exists(session.RecoveryPackagePath));
            Assert.NotEqual(database.Location.DatabasePath, session.RecoveryPackagePath);
            TemplatePackageContent restored = await coordinator.RestoreAsync(session);
            Assert.Equal(content.Document.Id, restored.Document.Id);
            await coordinator.DiscardAsync(session);
            Assert.False(File.Exists(session.RecoveryPackagePath));
            coordinator.QueueAutosave(content, null, TimeSpan.FromMilliseconds(10));
            await Task.Delay(150);
            Assert.Single(await coordinator.GetAvailableAsync());
            coordinator.Dispose();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }
}
