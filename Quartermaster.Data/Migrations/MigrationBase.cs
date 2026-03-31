using FluentMigrator;

namespace Quartermaster.Data.Migrations;

public abstract class MigrationBase : Migration {
    protected void DropTableIfExists(string tableName)
        => Execute.Sql($"DROP TABLE IF EXISTS `{tableName}`");

    protected void DisableForeignKeyChecks()
        => Execute.Sql("SET FOREIGN_KEY_CHECKS=0");

    protected void EnableForeignKeyChecks()
        => Execute.Sql("SET FOREIGN_KEY_CHECKS=1");
}
