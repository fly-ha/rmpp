using System.Text;
using Microsoft.Data.Sqlite;
using Rmpp.Application.Data;
using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Data;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Templates;
using Rmpp.Infrastructure.Tests.Persistence;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Data;

public sealed class ImportedDataPrivacyTests
{
    [Fact]
    public async Task ImportedRowsDoNotEnterTemplatePackageOrSqliteState()
    {
        const string secret = "PRIVATE-CUSTOMER-ROW-92741";
        string csvPath = Path.Combine(Path.GetTempPath(), $"rmpp-private-{Guid.NewGuid():N}.csv");
        await using PersistenceTestDirectory directory = PersistenceTestDirectory.Create();
        try
        {
            await File.WriteAllTextAsync(csvPath, $"Name\n{secret}\n", Encoding.UTF8);
            DataSetSnapshot imported = await new CsvDataSourceReader().ReadAsync(new DataImportOptions
            {
                FilePath = csvPath,
            });
            Assert.Equal(secret, Assert.Single(imported.Rows).GetValue("Name"));
            Assert.DoesNotContain(secret, imported.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret, imported.Rows[0].ToString(), StringComparison.Ordinal);

            await using MemoryStream package = new();
            await new RmppPackageWriter().WriteAsync(package, new Rmpp.Infrastructure.Templates.TemplatePackageContent
            {
                Document = TemplateDocument.CreateNew("Privacy"),
            });
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(package.ToArray()), StringComparison.Ordinal);

            SqliteAppDatabase database = PersistenceTestDatabase.Create(directory.Path);
            await database.InitializeAsync();
            SqliteConnection.ClearAllPools();
            await using FileStream databaseFile = new(
                database.Location.DatabasePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using MemoryStream databaseCopy = new();
            await databaseFile.CopyToAsync(databaseCopy);
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(databaseCopy.ToArray()), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }
}
