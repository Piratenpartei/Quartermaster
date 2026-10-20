using FluentMigrator;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Data.Migrations;

[Migration(012)]
public class M007_3 : MigrationBase {
    public override void Up() {
        Alter.Table(AssemblyMotionType.TableName)
            .AddColumn(nameof(AssemblyMotionType.DefaultText)).AsCustom("TEXT").NotNullable().WithDefaultValue("");
    }

    public override void Down() {
        Delete.Column(nameof(AssemblyMotionType.DefaultText)).FromTable(AssemblyMotionType.TableName);
    }
}
