using FluentMigrator;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;

namespace Quartermaster.Data.Migrations;

[Migration(010)]
public class M007 : MigrationBase {
    public override void Up() {
        Create.Table(RedmineConnection.TableName)
            .WithColumn(nameof(RedmineConnection.ChapterId)).AsGuid().PrimaryKey()
            .WithColumn(nameof(RedmineConnection.BaseUrl)).AsString(RedmineConnection.BaseUrlMaxLength).NotNullable()
            .WithColumn(nameof(RedmineConnection.ApiKey)).AsString(RedmineConnection.ApiKeyMaxLength).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(RedmineConnection.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_RedmineConnections_ChapterId_Chapters_Id")
            .FromTable(RedmineConnection.TableName).ForeignColumn(nameof(RedmineConnection.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(ForwardingTarget.TableName)
            .WithColumn(nameof(ForwardingTarget.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ForwardingTarget.ChapterId)).AsGuid().NotNullable()
            .WithColumn(nameof(ForwardingTarget.Purpose)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingTarget.ProjectId)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingTarget.TrackerId)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingTarget.PriorityId)).AsInt32().Nullable()
            .WithColumn(nameof(ForwardingTarget.AssigneeId)).AsInt32().Nullable()
            .WithColumn(nameof(ForwardingTarget.CustomFieldsJson)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(ForwardingTarget.NoteStatusId)).AsInt32().Nullable()
            .WithColumn(nameof(ForwardingTarget.NoteAssigneeId)).AsInt32().Nullable();

        Create.ForeignKey("FK_ForwardingTargets_ChapterId_Chapters_Id")
            .FromTable(ForwardingTarget.TableName).ForeignColumn(nameof(ForwardingTarget.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Index("UX_ForwardingTargets_ChapterId_Purpose")
            .OnTable(ForwardingTarget.TableName)
            .OnColumn(nameof(ForwardingTarget.ChapterId)).Ascending()
            .OnColumn(nameof(ForwardingTarget.Purpose)).Ascending()
            .WithOptions().Unique();

        Create.Table(ForwardingChapterSetting.TableName)
            .WithColumn(nameof(ForwardingChapterSetting.ChapterId)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ForwardingChapterSetting.ApplicationMode)).AsInt32().Nullable()
            .WithColumn(nameof(ForwardingChapterSetting.ReductionMode)).AsInt32().Nullable()
            .WithColumn(nameof(ForwardingChapterSetting.FailureEmail)).AsString(ForwardingChapterSetting.FailureEmailMaxLength).Nullable();

        Create.ForeignKey("FK_ForwardingChapterSettings_ChapterId_Chapters_Id")
            .FromTable(ForwardingChapterSetting.TableName).ForeignColumn(nameof(ForwardingChapterSetting.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(ForwardingCase.TableName)
            .WithColumn(nameof(ForwardingCase.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ForwardingCase.Kind)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingCase.ApplicationChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.DecidingChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.StateChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.RelatedCaseId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.MembershipApplicationId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.DueSelectionId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.MotionId)).AsGuid().Nullable()
            .WithColumn(nameof(ForwardingCase.PayloadJson)).AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn(nameof(ForwardingCase.Status)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(ForwardingCase.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ForwardingCase.CompletedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.Index("IX_ForwardingCases_MotionId")
            .OnTable(ForwardingCase.TableName)
            .OnColumn(nameof(ForwardingCase.MotionId));

        Create.Index("IX_ForwardingCases_Status_CreatedAt")
            .OnTable(ForwardingCase.TableName)
            .OnColumn(nameof(ForwardingCase.Status)).Ascending()
            .OnColumn(nameof(ForwardingCase.CreatedAt)).Ascending();

        Create.Table(ForwardingStep.TableName)
            .WithColumn(nameof(ForwardingStep.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ForwardingStep.CaseId)).AsGuid().NotNullable()
            .WithColumn(nameof(ForwardingStep.Order)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingStep.Kind)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingStep.TargetChapterId)).AsGuid().NotNullable()
            .WithColumn(nameof(ForwardingStep.Purpose)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingStep.State)).AsInt32().NotNullable()
            .WithColumn(nameof(ForwardingStep.TicketId)).AsInt32().Nullable()
            .WithColumn(nameof(ForwardingStep.TicketUrl)).AsString(ForwardingStep.TicketUrlMaxLength).Nullable()
            .WithColumn(nameof(ForwardingStep.Attempts)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(ForwardingStep.NextAttemptAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(ForwardingStep.LastError)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(ForwardingStep.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_ForwardingSteps_CaseId_ForwardingCases_Id")
            .FromTable(ForwardingStep.TableName).ForeignColumn(nameof(ForwardingStep.CaseId))
            .ToTable(ForwardingCase.TableName).PrimaryColumn(nameof(ForwardingCase.Id));

        Create.Index("IX_ForwardingSteps_State_NextAttemptAt")
            .OnTable(ForwardingStep.TableName)
            .OnColumn(nameof(ForwardingStep.State)).Ascending()
            .OnColumn(nameof(ForwardingStep.NextAttemptAt)).Ascending();

        Alter.Table(DueSelection.TableName)
            .AddColumn(nameof(DueSelection.ChapterId)).AsGuid().Nullable();

        Create.ForeignKey("FK_DueSelections_ChapterId_Chapters_Id")
            .FromTable(DueSelection.TableName).ForeignColumn(nameof(DueSelection.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));
    }

    public override void Down() {
        Delete.ForeignKey("FK_DueSelections_ChapterId_Chapters_Id").OnTable(DueSelection.TableName);
        Delete.Column(nameof(DueSelection.ChapterId)).FromTable(DueSelection.TableName);
        DropTableIfExists(ForwardingStep.TableName);
        DropTableIfExists(ForwardingCase.TableName);
        DropTableIfExists(ForwardingChapterSetting.TableName);
        DropTableIfExists(ForwardingTarget.TableName);
        DropTableIfExists(RedmineConnection.TableName);
    }
}
