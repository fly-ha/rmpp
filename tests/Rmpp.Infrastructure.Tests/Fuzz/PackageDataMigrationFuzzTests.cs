using System.IO.Compression;
using System.Text;
using Rmpp.Application.Data;
using Rmpp.Infrastructure.Data;
using Rmpp.Infrastructure.Templates;
using Rmpp.Infrastructure.Tests.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Fuzz;

public sealed class PackageDataMigrationFuzzTests
{
    [Fact]
    public async Task RandomTraversalPathsAreRejectedWithoutWritingFiles()
    {
        Random random = new(2026);
        for (int iteration = 0; iteration < 80; iteration++)
        {
            string path = iteration % 2 == 0
                ? $"folder/{new string('a', random.Next(1, 20))}/../../escape{iteration}.txt"
                : $"C:/escape{iteration}.txt";
            await using MemoryStream stream = new();
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                Write(archive, path, "x");
                Write(archive, "manifest.json", "{}");
                Write(archive, "document.json", "{}");
            }
            stream.Position = 0;
            await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
        }
    }

    [Fact]
    public async Task RandomElementDiscriminatorsAreRejected()
    {
        Random random = new(99);
        for (int iteration = 0; iteration < 40; iteration++)
        {
            await using MemoryStream stream = await PackageArchiveTestHelper.CreatePackageAsync();
            string discriminator = "unknown-" + random.NextInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
            PackageArchiveTestHelper.ReplaceTextEntry(stream, "document.json", json => json.Replace("\"$type\": \"polygon\"", $"\"$type\": \"{discriminator}\"", StringComparison.Ordinal));
            stream.Position = 0;
            await Assert.ThrowsAsync<RmppPackageException>(() => new RmppPackageReader().ReadAsync(stream));
        }
    }

    [Fact]
    public async Task RandomQuotedCsvRowsRoundTrip()
    {
        Random random = new(1234);
        for (int iteration = 0; iteration < 30; iteration++)
        {
            string value = $"值,{random.Next()} \"引号\"\n第{iteration}行";
            string escaped = "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            string path = Path.Combine(Path.GetTempPath(), "rmpp-fuzz-" + Guid.NewGuid().ToString("N") + ".csv");
            await File.WriteAllTextAsync(path, "field\n" + escaped, Encoding.UTF8);
            try
            {
                DataSetSnapshot data = await new CsvDataSourceReader().ReadAsync(new DataImportOptions { FilePath = path });
                Assert.Equal(value, Assert.Single(data.Rows).GetValue("field"));
            }
            finally { File.Delete(path); }
        }
    }

    [Fact]
    public void RandomInvalidMigrationRangesFailClosed()
    {
        Random random = new(456);
        SchemaMigrationRegistry registry = new();
        var document = TemplatePackageTestData.Create().Document;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            int from = random.Next(-10, 10);
            int target = random.Next(-10, 10);
            if (from == target && from > 0)
            {
                Assert.Same(document, registry.Migrate(document, from, target));
            }
            else
            {
                Assert.Throws<RmppPackageException>(() => registry.Migrate(document, from, target));
            }
        }
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using Stream output = entry.Open();
        output.Write(Encoding.UTF8.GetBytes(content));
    }
}
