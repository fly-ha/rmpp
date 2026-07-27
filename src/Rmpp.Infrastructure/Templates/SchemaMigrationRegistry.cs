using Rmpp.Domain.Documents;

namespace Rmpp.Infrastructure.Templates;

/// <summary>按连续版本顺序执行迁移，禁止跳过未知版本。</summary>
public sealed class SchemaMigrationRegistry
{
    private readonly Dictionary<int, ITemplateSchemaMigration> migrations;

    public SchemaMigrationRegistry(IEnumerable<ITemplateSchemaMigration>? migrations = null)
    {
        this.migrations = (migrations ?? Array.Empty<ITemplateSchemaMigration>())
            .ToDictionary(static migration => migration.FromVersion);
    }

    public TemplateDocument Migrate(TemplateDocument document, int fromVersion, int targetVersion)
    {
        if (fromVersion <= 0 || targetVersion < fromVersion)
        {
            throw new RmppPackageException("Invalid template schema migration range.");
        }

        TemplateDocument current = document;
        for (int version = fromVersion; version < targetVersion; version++)
        {
            if (!migrations.TryGetValue(version, out ITemplateSchemaMigration? migration) || migration.ToVersion != version + 1)
            {
                throw new RmppPackageException($"No migration is registered from schema version {version}.");
            }

            current = migration.Migrate(current);
        }

        return current;
    }
}
