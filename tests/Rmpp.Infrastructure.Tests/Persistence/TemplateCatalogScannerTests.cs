using Microsoft.Data.Sqlite;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;
using Rmpp.Infrastructure.Templates;
using Rmpp.Infrastructure.Tests.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Persistence;

public sealed class TemplateCatalogScannerTests
{
    [Fact]
    public async Task ScannerRebuildsCatalogueAndMarksRemovedTemplatesMissing()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        string templateDirectory = Path.Combine(directory.Path, "templates");
        Directory.CreateDirectory(templateDirectory);
        string firstPath = Path.Combine(templateDirectory, "first.rmpp");
        string secondPath = Path.Combine(templateDirectory, "second.rmpp");
        await new AtomicTemplateFileWriter().WriteAsync(firstPath, TemplatePackageTestData.Create());
        await new AtomicTemplateFileWriter().WriteAsync(secondPath, TemplatePackageTestData.Create());

        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        await database.InitializeAsync();
        TemplateCatalogRepository repository = new(database);
        TemplateCatalogScanner scanner = new(repository);
        TemplateScanResult firstScan = await scanner.ScanAsync([templateDirectory]);

        Assert.Equal(2, firstScan.ValidTemplates);
        Assert.Equal(2, (await repository.GetAllAsync()).Count);
        File.Delete(secondPath);
        await scanner.ScanAsync([templateDirectory]);
        IReadOnlyList<TemplateCatalogEntry> entries = await repository.GetAllAsync();
        Assert.Contains(entries, entry =>
            string.Equals(entry.Path, secondPath, StringComparison.OrdinalIgnoreCase) &&
            entry.Status == TemplateCatalogStatus.Missing);
    }

    [Fact]
    public async Task DeletingDatabaseDoesNotPreventDirectTemplateOpening()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        string templatePath = Path.Combine(directory.Path, "independent.rmpp");
        await new AtomicTemplateFileWriter().WriteAsync(templatePath, TemplatePackageTestData.Create());
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        await database.InitializeAsync();

        SqliteConnection.ClearAllPools();
        File.Delete(database.Location.DatabasePath);
        await using FileStream stream = File.OpenRead(templatePath);
        TemplatePackageContent loaded = await new RmppPackageReader().ReadAsync(stream);

        Assert.Equal("Package test", loaded.Document.Metadata.Title);
    }
}
