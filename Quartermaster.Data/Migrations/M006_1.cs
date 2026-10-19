using FluentMigrator;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Data.Migrations;

[Migration(009)]
public class M006_1 : MigrationBase {
    public override void Up() {
        Alter.Table(AssemblyMotion.TableName)
            .AddColumn(nameof(AssemblyMotion.ForumTopicId)).AsInt32().Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumPostId)).AsInt32().Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumSyncState)).AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn(nameof(AssemblyMotion.ForumSyncedRevision)).AsInt32().Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumSyncedHash)).AsString(64).Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumLastSyncedAt)).AsCustom("DATETIME(6)").Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumLastError)).AsCustom("TEXT").Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumFailureCount)).AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn(nameof(AssemblyMotion.ForumNextAttemptAt)).AsCustom("DATETIME(6)").Nullable()
            .AddColumn(nameof(AssemblyMotion.ForumSyncRequestedAt)).AsCustom("DATETIME(6)").Nullable();

        Alter.Table(AssemblyMotionPortal.TableName)
            .AddColumn(nameof(AssemblyMotionPortal.ForumMirrorEnabled)).AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn(nameof(AssemblyMotionPortal.ForumCategoryId)).AsInt32().Nullable();
    }

    public override void Down() {
        Delete.Column(nameof(AssemblyMotionPortal.ForumMirrorEnabled))
            .Column(nameof(AssemblyMotionPortal.ForumCategoryId))
            .FromTable(AssemblyMotionPortal.TableName);
        Delete.Column(nameof(AssemblyMotion.ForumTopicId))
            .Column(nameof(AssemblyMotion.ForumPostId))
            .Column(nameof(AssemblyMotion.ForumSyncState))
            .Column(nameof(AssemblyMotion.ForumSyncedRevision))
            .Column(nameof(AssemblyMotion.ForumSyncedHash))
            .Column(nameof(AssemblyMotion.ForumLastSyncedAt))
            .Column(nameof(AssemblyMotion.ForumLastError))
            .Column(nameof(AssemblyMotion.ForumFailureCount))
            .Column(nameof(AssemblyMotion.ForumNextAttemptAt))
            .Column(nameof(AssemblyMotion.ForumSyncRequestedAt))
            .FromTable(AssemblyMotion.TableName);
    }
}
