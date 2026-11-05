using FluentMigrator;
using Quartermaster.Data.Templates;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Migrations;

[Migration(018)]
public class M009_1 : MigrationBase {
    public override void Up() {
        Alter.Table(Template.TableName)
            .AddColumn(nameof(Template.SeedHash)).AsString(TemplateSeedHash.Length).Nullable();

        Alter.Table(User.TableName)
            .AlterColumn(nameof(User.Username)).AsString(User.UsernameMaxLength).Nullable();
        Alter.Table(User.TableName)
            .AddColumn(nameof(User.DisplayNameMode)).AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down() {
        Delete.Column(nameof(User.DisplayNameMode)).FromTable(User.TableName);
        Alter.Table(User.TableName)
            .AlterColumn(nameof(User.Username)).AsString(64).Nullable();
        Delete.Column(nameof(Template.SeedHash)).FromTable(Template.TableName);
    }
}
