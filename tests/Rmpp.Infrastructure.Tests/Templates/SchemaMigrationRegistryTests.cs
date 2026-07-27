using Rmpp.Domain.Documents;
using Rmpp.Infrastructure.Templates;
using Xunit;

namespace Rmpp.Infrastructure.Tests.Templates;

public sealed class SchemaMigrationRegistryTests
{
    [Fact]
    public void RegisteredMigrationChainRunsInVersionOrder()
    {
        TemplateDocument source = TemplateDocument.CreateNew("v1");
        SchemaMigrationRegistry registry = new(
        [
            new RenameMigration(1, 2, "v2"),
            new RenameMigration(2, 3, "v3"),
        ]);

        TemplateDocument result = registry.Migrate(source, fromVersion: 1, targetVersion: 3);

        Assert.Equal("v3", result.Metadata.Title);
    }

    [Fact]
    public void MissingMigrationStepIsRejected()
    {
        TemplateDocument source = TemplateDocument.CreateNew("v1");
        SchemaMigrationRegistry registry = new([new RenameMigration(1, 2, "v2")]);

        Assert.Throws<RmppPackageException>(() => registry.Migrate(source, fromVersion: 1, targetVersion: 3));
    }

    private sealed class RenameMigration(int fromVersion, int toVersion, string title) : ITemplateSchemaMigration
    {
        public int FromVersion { get; } = fromVersion;
        public int ToVersion { get; } = toVersion;

        public TemplateDocument Migrate(TemplateDocument document) =>
            document with { Metadata = document.Metadata with { Title = title } };
    }
}
