using FluentMigrator;
using Quartermaster.Data.Templates;

namespace Quartermaster.Data.Migrations;

[Migration(018)]
public class M009_1 : MigrationBase {
    public override void Up() {
        Alter.Table(Template.TableName)
            .AddColumn(nameof(Template.SeedHash)).AsString(TemplateSeedHash.Length).Nullable();
    }

    public override void Down() {
        Delete.Column(nameof(Template.SeedHash)).FromTable(Template.TableName);
    }
}
