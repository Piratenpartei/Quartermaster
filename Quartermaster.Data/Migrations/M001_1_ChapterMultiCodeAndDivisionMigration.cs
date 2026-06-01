using FluentMigrator;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Data.Migrations;

[Migration(002)]
public class M001_1_ChapterMultiCodeAndDivisionMigration : MigrationBase {
    public override void Up() {
        Alter.Table(Chapter.TableName)
            .AddColumn(nameof(Chapter.ArchivedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.Table(ChapterExternalCode.TableName)
            .WithColumn(nameof(ChapterExternalCode.ChapterId)).AsGuid().NotNullable()
            .WithColumn(nameof(ChapterExternalCode.Code)).AsString(128).NotNullable();

        Create.PrimaryKey("PK_ChapterExternalCodes")
            .OnTable(ChapterExternalCode.TableName)
            .Columns(nameof(ChapterExternalCode.ChapterId), nameof(ChapterExternalCode.Code));

        Create.Index("IX_ChapterExternalCodes_Code")
            .OnTable(ChapterExternalCode.TableName)
            .OnColumn(nameof(ChapterExternalCode.Code));

        Create.ForeignKey("FK_ChapterExternalCodes_ChapterId_Chapters_Id")
            .FromTable(ChapterExternalCode.TableName).ForeignColumn(nameof(ChapterExternalCode.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id))
            .OnDelete(System.Data.Rule.Cascade);

        Create.Table(ChapterAdministrativeDivision.TableName)
            .WithColumn(nameof(ChapterAdministrativeDivision.ChapterId)).AsGuid().NotNullable()
            .WithColumn(nameof(ChapterAdministrativeDivision.AdministrativeDivisionId)).AsGuid().NotNullable();

        Create.PrimaryKey("PK_ChapterAdministrativeDivisions")
            .OnTable(ChapterAdministrativeDivision.TableName)
            .Columns(nameof(ChapterAdministrativeDivision.ChapterId), nameof(ChapterAdministrativeDivision.AdministrativeDivisionId));

        Create.Index("IX_ChapterAdministrativeDivisions_DivisionId")
            .OnTable(ChapterAdministrativeDivision.TableName)
            .OnColumn(nameof(ChapterAdministrativeDivision.AdministrativeDivisionId));

        Create.ForeignKey("FK_ChapterAdministrativeDivisions_ChapterId_Chapters_Id")
            .FromTable(ChapterAdministrativeDivision.TableName).ForeignColumn(nameof(ChapterAdministrativeDivision.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id))
            .OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("FK_ChapterAdministrativeDivisions_AdministrativeDivisionId")
            .FromTable(ChapterAdministrativeDivision.TableName).ForeignColumn(nameof(ChapterAdministrativeDivision.AdministrativeDivisionId))
            .ToTable("AdministrativeDivisions").PrimaryColumn("Id");

        // Backfill: copy every non-empty scalar value into the new join tables before dropping the columns.
        Execute.Sql(@"
            INSERT INTO `ChapterExternalCodes` (`ChapterId`, `Code`)
            SELECT `Id`, `ExternalCode` FROM `Chapters`
            WHERE `ExternalCode` IS NOT NULL AND `ExternalCode` <> '';
        ");
        Execute.Sql(@"
            INSERT INTO `ChapterAdministrativeDivisions` (`ChapterId`, `AdministrativeDivisionId`)
            SELECT `Id`, `AdministrativeDivisionId` FROM `Chapters`
            WHERE `AdministrativeDivisionId` IS NOT NULL;
        ");

        Delete.ForeignKey("FK_Chapters_AdministrativeDivisionId_AdministrativeDivisions_Id").OnTable(Chapter.TableName);
        Delete.Column("AdministrativeDivisionId").FromTable(Chapter.TableName);
        Delete.Column("ExternalCode").FromTable(Chapter.TableName);
    }

    public override void Down() {
        DropTableIfExists(ChapterExternalCode.TableName);
        DropTableIfExists(ChapterAdministrativeDivision.TableName);
    }
}
