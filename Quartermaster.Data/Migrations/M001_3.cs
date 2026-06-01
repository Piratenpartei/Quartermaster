using FluentMigrator;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Data.Migrations;

[Migration(003)]
public class M001_3 : MigrationBase {
    public override void Up() {
        Alter.Column(nameof(AdministrativeDivision.PostCodes))
            .OnTable(AdministrativeDivision.TableName)
            .AsCustom("TEXT").Nullable();
    }

    public override void Down() {
        Alter.Column(nameof(AdministrativeDivision.PostCodes))
            .OnTable(AdministrativeDivision.TableName)
            .AsString(2048).Nullable();
    }
}
