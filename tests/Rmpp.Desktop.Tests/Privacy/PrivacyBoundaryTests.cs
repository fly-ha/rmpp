using System.IO;
using Microsoft.Data.Sqlite;
using Rmpp.Application.Data;
using Rmpp.Desktop.Composition;
using Rmpp.Desktop.Services;
using Rmpp.Desktop.ViewModels;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Desktop.Tests.Privacy;

public sealed class PrivacyBoundaryTests
{
    [Fact]
    public async Task ImportedMarkerDoesNotEnterTemplateSqliteRecoveryLogsOrCrashMaterial()
    {
        string marker = "CUSTOMER-SECRET-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "rmpp-privacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            DataSetSnapshot data = new()
            {
                SourceDisplayName = "customers.csv",
                Schema = new DataSchema { Columns = [new DataColumnDefinition("private", 0)] },
                Rows = [new DataRowSnapshot { Index = 0, Values = new Dictionary<string, string?> { ["private"] = marker } }],
            };
            using DocumentTabViewModel tab = new(TemplateDocument.CreateNew("隐私模板"));
            tab.AttachDataSet(data);

            SqliteAppDatabase database = new(new DatabaseOptions
            {
                StorageMode = DatabaseStorageMode.Installed,
                InstalledDataRoot = root,
                ApplicationDirectoryName = "state",
            });
            await database.InitializeAsync();
            AppStoragePaths paths = new(Path.Combine(root, "app"), false);
            Directory.CreateDirectory(paths.RecoveryDirectory);
            using RecoveryCoordinator recovery = new(paths, new RecoverySessionRepository(database), new AtomicTemplateFileWriter(), new RmppPackageReader());
            await recovery.SaveAsync(tab.CreateRecoveryContent(), null);
            await new AtomicTemplateFileWriter().WriteAsync(Path.Combine(root, "saved.rmpp"), tab.CreateRecoveryContent());
            await new RecentFileRepository(database).AddAsync(new Rmpp.Infrastructure.Persistence.Models.RecentFileEntry
            {
                Kind = Rmpp.Infrastructure.Persistence.Models.RecentFileKind.DataSource,
                Path = Path.Combine(root, "customers.csv"),
                DisplayName = "customers.csv",
                LastOpenedAt = DateTimeOffset.UtcNow,
            });
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            await File.WriteAllTextAsync(Path.Combine(root, "logs", "diagnostic.log"), "operation=data-import rows=1 columns=1");
            await File.WriteAllTextAsync(Path.Combine(root, "crash.txt"), "DataImportException: malformed row; values redacted");

            SqliteConnection.ClearAllPools();
            byte[] markerBytes = System.Text.Encoding.UTF8.GetBytes(marker);
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                byte[] bytes = await File.ReadAllBytesAsync(file);
                Assert.False(bytes.AsSpan().IndexOf(markerBytes) >= 0, $"敏感标记出现在 {file}");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }
}
