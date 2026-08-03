using System.IO;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Persistence;

public sealed class RepositoryRemovalTests
{
    [Fact]
    public async Task TemplateDeletionRemovesCatalogAndRecentMetadataByNormalizedPath()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        await database.InitializeAsync();
        string path = Path.Combine(directory.Path, "template.rmpp");
        TemplateCatalogRepository catalog = new(database);
        RecentFileRepository recent = new(database);
        await catalog.UpsertAsync(new TemplateCatalogEntry
        {
            DocumentId = Guid.NewGuid(),
            Path = path,
            Title = "template",
            FileModifiedAt = DateTimeOffset.UtcNow,
            DocumentModifiedAt = DateTimeOffset.UtcNow,
            LastScannedAt = DateTimeOffset.UtcNow,
            Status = TemplateCatalogStatus.Available,
        });
        await recent.AddAsync(new RecentFileEntry
        {
            Kind = RecentFileKind.Template,
            Path = path,
            DisplayName = "template",
            LastOpenedAt = DateTimeOffset.UtcNow,
        });

        await catalog.RemoveAsync(path);
        await recent.RemoveAsync(RecentFileKind.Template, path);

        Assert.Empty(await catalog.GetAllAsync());
        Assert.Empty(await recent.GetAsync(RecentFileKind.Template));
    }
}
