using Microsoft.Data.Sqlite;
using Rmpp.Domain.Geometry;
using Rmpp.Domain.Printing;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Persistence.Models;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Persistence;

public sealed class RepositoryIntegrationTests
{
    [Fact]
    public async Task CatalogueDetectsDuplicateDocumentIdentityAndPreservesTags()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        await database.InitializeAsync();
        TemplateCatalogRepository repository = new(database);
        Guid documentId = Guid.NewGuid();

        await repository.UpsertAsync(CreateCatalogEntry(documentId, Path.Combine(directory.Path, "one.rmpp"), ["forms", "A4"]));
        await repository.UpsertAsync(CreateCatalogEntry(documentId, Path.Combine(directory.Path, "two.rmpp"), ["copy"]));

        IReadOnlyList<TemplateCatalogEntry> entries = await repository.GetAllAsync();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, static entry => Assert.Equal(TemplateCatalogStatus.DuplicateIdentity, entry.Status));
        Assert.Contains(entries, static entry => entry.Tags.Contains("A4", StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecentFilesAreBoundedAndStoreNoImportedRows()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        await database.InitializeAsync();
        RecentFileRepository repository = new(database);
        for (int index = 0; index < 5; index++)
        {
            await repository.AddAsync(new RecentFileEntry
            {
                Kind = RecentFileKind.DataSource,
                Path = Path.Combine(directory.Path, $"data-{index}.csv"),
                DisplayName = $"data-{index}.csv",
                LastOpenedAt = DateTimeOffset.UtcNow.AddMinutes(index),
            }, maximumEntriesPerKind: 3);
        }

        IReadOnlyList<RecentFileEntry> entries = await repository.GetAsync(RecentFileKind.DataSource);
        Assert.Equal(3, entries.Count);
        await using SqliteConnection connection = await database.OpenConnectionAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM recent_files WHERE metadata_json IS NOT NULL;";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? -1L));
    }

    [Fact]
    public async Task CalibrationSettingsAndRecoveryRoundTrip()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
        await database.InitializeAsync();

        CalibrationProfileRepository calibrationRepository = new(database);
        PrinterMediaKey key = new("printer-1", "a4");
        CalibrationProfile profile = new()
        {
            Key = key,
            OffsetMm = new MmPoint(1.2, -0.8),
            ScaleX = 1.001,
            ScaleY = 0.999,
            RotationDegrees = 0.1,
            LastVerifiedAt = DateTimeOffset.UtcNow,
        };
        await calibrationRepository.SaveAsync(profile);
        Assert.Equal(profile.OffsetMm, (await calibrationRepository.GetAsync(key))?.OffsetMm);

        SettingsRepository settingsRepository = new(database);
        SettingDefinition<double> zoom = new("editor.zoom", 1.0, 1, static value => value is >= 0.1 and <= 10);
        await settingsRepository.SetAsync(zoom, 2.5);
        SettingReadResult<double> valid = await settingsRepository.GetAsync(zoom);
        Assert.False(valid.UsedDefault);
        Assert.Equal(2.5, valid.Value);
        await using (SqliteConnection connection = await database.OpenConnectionAsync())
        await using (SqliteCommand corrupt = connection.CreateCommand())
        {
            corrupt.CommandText = "UPDATE app_settings SET value_json = '999' WHERE setting_key = 'editor.zoom';";
            await corrupt.ExecuteNonQueryAsync();
        }

        SettingReadResult<double> invalid = await settingsRepository.GetAsync(zoom);
        Assert.True(invalid.UsedDefault);
        Assert.Equal(1.0, invalid.Value);

        RecoverySessionRepository recoveryRepository = new(database);
        RecoverySession session = new()
        {
            SessionId = Guid.NewGuid(),
            DocumentId = Guid.NewGuid(),
            OriginalTemplatePath = Path.Combine(directory.Path, "original.rmpp"),
            RecoveryPackagePath = Path.Combine(directory.Path, "recovery.rmpp"),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            State = RecoverySessionState.Available,
        };
        await recoveryRepository.SaveAsync(session);
        Assert.Single(await recoveryRepository.GetAvailableAsync());
        await recoveryRepository.SetStateAsync(session.SessionId, RecoverySessionState.Restored);
        Assert.Empty(await recoveryRepository.GetAvailableAsync());
    }

    [Fact]
    public async Task TransientWriteLockIsRetriedWithoutLosingUpdate()
    {
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path, TimeSpan.FromMilliseconds(50));
        await database.InitializeAsync();
        RecentFileRepository repository = new(database);
        await using SqliteConnection blocker = await database.OpenConnectionAsync();
        await using SqliteTransaction transaction = blocker.BeginTransaction(deferred: false);

        Task write = repository.AddAsync(new RecentFileEntry
        {
            Kind = RecentFileKind.Template,
            Path = Path.Combine(directory.Path, "locked.rmpp"),
            DisplayName = "locked.rmpp",
            LastOpenedAt = DateTimeOffset.UtcNow,
        });
        await Task.Delay(180);
        await transaction.CommitAsync();
        await write;

        Assert.Single(await repository.GetAsync(RecentFileKind.Template));
    }

    private static TemplateCatalogEntry CreateCatalogEntry(Guid documentId, string path, IReadOnlyList<string> tags) =>
        new()
        {
            DocumentId = documentId,
            Path = path,
            Title = Path.GetFileNameWithoutExtension(path),
            FileModifiedAt = DateTimeOffset.UtcNow,
            DocumentModifiedAt = DateTimeOffset.UtcNow,
            LastScannedAt = DateTimeOffset.UtcNow,
            Status = TemplateCatalogStatus.Available,
            Tags = tags,
        };
}
