using System.Data;
using FluentMigrator;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Data.Migrations;

[Migration(016)]
public class M008_5 : MigrationBase {
    public override void Up() {
        Create.Table(AdministrativeDivisionName.TableName)
            .WithColumn(nameof(AdministrativeDivisionName.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AdministrativeDivisionName.AdministrativeDivisionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AdministrativeDivisionName.Name)).AsString(AdministrativeDivisionName.NameMaxLength).NotNullable()
            .WithColumn(nameof(AdministrativeDivisionName.Language)).AsString(AdministrativeDivisionName.LanguageMaxLength).Nullable()
            .WithColumn(nameof(AdministrativeDivisionName.Source)).AsInt32().NotNullable()
            .WithColumn(nameof(AdministrativeDivisionName.IsPreferred)).AsBoolean().NotNullable()
            .WithColumn(nameof(AdministrativeDivisionName.IsHistoric)).AsBoolean().NotNullable();

        Create.ForeignKey("FK_AdministrativeDivisionNames_DivisionId")
            .FromTable(AdministrativeDivisionName.TableName).ForeignColumn(nameof(AdministrativeDivisionName.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id))
            .OnDelete(Rule.Cascade);
    }

    public override void Down() {
        Delete.Table(AdministrativeDivisionName.TableName);
    }
}
