using FluentMigrator;
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
    }

    public override void Down() {
        Delete.Column(nameof(Member.IsImported)).FromTable(Member.TableName);
    }
}
