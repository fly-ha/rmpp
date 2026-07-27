using Microsoft.Data.Sqlite;

namespace Rmpp.Infrastructure.Persistence.Migrations;

/// <summary>定义按整数版本连续执行的事务内SQLite模式迁移。</summary>
public interface IDatabaseMigration
{
    int Version { get; }
    string Name { get; }
    Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken);
}
