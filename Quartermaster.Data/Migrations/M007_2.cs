using FluentMigrator;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Members;

namespace Quartermaster.Data.Migrations;

[Migration(011)]
public class M007_2 : MigrationBase {
    public override void Up() {
        Alter.Table(Member.TableName)
            .AddColumn(nameof(Member.IsImported)).AsBoolean().NotNullable().WithDefaultValue(false);

        // A member an SSO login created carries only names, email and number until the import delivers it.
        Execute.Sql($@"
            UPDATE `{Member.TableName}` SET `{nameof(Member.IsImported)}` = 1
            WHERE `{nameof(Member.AnonymizedAt)}` IS NOT NULL
                OR `{nameof(Member.MemberNumber)}` IS NULL
                OR `{nameof(Member.Street)}` IS NOT NULL
                OR `{nameof(Member.PostCode)}` IS NOT NULL
                OR `{nameof(Member.DateOfBirth)}` IS NOT NULL
                OR `{nameof(Member.EntryDate)}` IS NOT NULL");

        Alter.Table(AssemblyMotion.TableName)
            .AddColumn(nameof(AssemblyMotion.ReferenceNumber)).AsInt32().Nullable();

        Execute.Sql($@"
            UPDATE `{AssemblyMotion.TableName}` m
            JOIN (
                SELECT `Id`, ROW_NUMBER() OVER (PARTITION BY `PortalId` ORDER BY `CreatedAt`, `Id`) AS `Position`
                FROM `{AssemblyMotion.TableName}`
                WHERE `Status` <> 0 AND `DeletedAt` IS NULL
            ) numbered ON numbered.`Id` = m.`Id`
            SET m.`{nameof(AssemblyMotion.ReferenceNumber)}` = numbered.`Position`");

        Create.Index("IX_AssemblyMotions_Portal_ReferenceNumber")
            .OnTable(AssemblyMotion.TableName)
            .OnColumn(nameof(AssemblyMotion.PortalId)).Ascending()
            .OnColumn(nameof(AssemblyMotion.ReferenceNumber)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down() {
        Delete.Index("IX_AssemblyMotions_Portal_ReferenceNumber").OnTable(AssemblyMotion.TableName);
        Delete.Column(nameof(AssemblyMotion.ReferenceNumber)).FromTable(AssemblyMotion.TableName);
        Delete.Column(nameof(Member.IsImported)).FromTable(Member.TableName);
    }
}
