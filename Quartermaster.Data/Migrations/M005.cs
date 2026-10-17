using FluentMigrator;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.Events;

namespace Quartermaster.Data.Migrations;

[Migration(007)]
public class M005 : MigrationBase {
    public override void Up() {
        Create.Table(AssemblySession.TableName)
            .WithColumn(nameof(AssemblySession.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblySession.EventId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblySession.OpenSlidesMeetingId)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblySession.StartedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblySession.StartedByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblySession.EndedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblySession.LogDeletedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.ForeignKey("FK_AssemblySessions_EventId_Events_Id")
            .FromTable(AssemblySession.TableName).ForeignColumn(nameof(AssemblySession.EventId))
            .ToTable(Event.TableName).PrimaryColumn(nameof(Event.Id));

        Create.Index("IX_AssemblySessions_EndedAt")
            .OnTable(AssemblySession.TableName)
            .OnColumn(nameof(AssemblySession.EndedAt));

        Create.Table(AssemblyLogEntry.TableName)
            .WithColumn(nameof(AssemblyLogEntry.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyLogEntry.SessionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyLogEntry.At)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyLogEntry.Kind)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(AssemblyLogEntry.ItemTitle)).AsString(AssemblyLogEntry.ItemTitleMaxLength).Nullable()
            .WithColumn(nameof(AssemblyLogEntry.OpenSlidesAgendaItemId)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyLogEntry.MumbleName)).AsString(AssemblyLogEntry.MumbleNameMaxLength).Nullable()
            .WithColumn(nameof(AssemblyLogEntry.Role)).AsString(50).Nullable()
            .WithColumn(nameof(AssemblyLogEntry.StartedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyLogEntry.EndedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyLogEntry.AllottedSeconds)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyLogEntry.OpenSlidesPollId)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyLogEntry.Detail)).AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn(nameof(AssemblyLogEntry.ActorUserId)).AsGuid().Nullable();

        Create.ForeignKey("FK_AssemblyLogEntries_SessionId_AssemblySessions_Id")
            .FromTable(AssemblyLogEntry.TableName).ForeignColumn(nameof(AssemblyLogEntry.SessionId))
            .ToTable(AssemblySession.TableName).PrimaryColumn(nameof(AssemblySession.Id));

        Create.Index("IX_AssemblyLogEntries_Session_At")
            .OnTable(AssemblyLogEntry.TableName)
            .OnColumn(nameof(AssemblyLogEntry.SessionId)).Ascending()
            .OnColumn(nameof(AssemblyLogEntry.At)).Ascending();
    }

    public override void Down() {
        DropTableIfExists(AssemblyLogEntry.TableName);
        DropTableIfExists(AssemblySession.TableName);
    }
}
