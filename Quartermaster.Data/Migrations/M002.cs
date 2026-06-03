using FluentMigrator;
using Quartermaster.Data.Members;

namespace Quartermaster.Data.Migrations;

[Migration(004)]
public class M002 : MigrationBase {
    public override void Up() {
        Alter.Column(nameof(Member.MemberNumber))
            .OnTable(Member.TableName)
            .AsInt32().Nullable();

        Alter.Table(MemberImportLog.TableName)
            .AddColumn(nameof(MemberImportLog.SkippedRecords)).AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn(nameof(MemberImportLog.NewFloatingRecords)).AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn(nameof(MemberImportLog.UpdatedFloatingRecords)).AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn(nameof(MemberImportLog.PromotedRecords)).AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down() {
        Delete.Column(nameof(MemberImportLog.PromotedRecords)).FromTable(MemberImportLog.TableName);
        Delete.Column(nameof(MemberImportLog.UpdatedFloatingRecords)).FromTable(MemberImportLog.TableName);
        Delete.Column(nameof(MemberImportLog.NewFloatingRecords)).FromTable(MemberImportLog.TableName);
        Delete.Column(nameof(MemberImportLog.SkippedRecords)).FromTable(MemberImportLog.TableName);

        Alter.Column(nameof(Member.MemberNumber))
            .OnTable(Member.TableName)
            .AsInt32().NotNullable();
    }
}
