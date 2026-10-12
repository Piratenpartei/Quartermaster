using FluentMigrator;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;

namespace Quartermaster.Data.Migrations;

[Migration(006)]
public class M004 : MigrationBase {
    public override void Up() {
        Create.Table(AssemblyMotionPortal.TableName)
            .WithColumn(nameof(AssemblyMotionPortal.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionPortal.EventId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionPortal.OpensAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionPortal.ClosesAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionPortal.IntroMarkdown)).AsCustom("MEDIUMTEXT").NotNullable().WithDefaultValue("")
            .WithColumn(nameof(AssemblyMotionPortal.IntroHtml)).AsCustom("MEDIUMTEXT").NotNullable().WithDefaultValue("")
            .WithColumn(nameof(AssemblyMotionPortal.PrioritizationEnabled)).AsBoolean().NotNullable()
            .WithColumn(nameof(AssemblyMotionPortal.PrioritizationStartsAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotionPortal.PrioritizationEndsAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotionPortal.WikiMirrorEnabled)).AsBoolean().NotNullable()
            .WithColumn(nameof(AssemblyMotionPortal.WikiPagePrefix)).AsString(300).Nullable()
            .WithColumn(nameof(AssemblyMotionPortal.WikiAssemblyLabel)).AsString(100).Nullable()
            .WithColumn(nameof(AssemblyMotionPortal.OpenSlidesMeetingId)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyMotionPortal.CreatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_AssemblyMotionPortals_EventId_Events_Id")
            .FromTable(AssemblyMotionPortal.TableName).ForeignColumn(nameof(AssemblyMotionPortal.EventId))
            .ToTable(Event.TableName).PrimaryColumn(nameof(Event.Id));

        Create.Index("IX_AssemblyMotionPortals_EventId")
            .OnTable(AssemblyMotionPortal.TableName)
            .OnColumn(nameof(AssemblyMotionPortal.EventId)).Unique();

        Create.Table(AssemblyMotionType.TableName)
            .WithColumn(nameof(AssemblyMotionType.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionType.PortalId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionType.Code)).AsString(16).NotNullable()
            .WithColumn(nameof(AssemblyMotionType.Name)).AsString(200).NotNullable()
            .WithColumn(nameof(AssemblyMotionType.SortOrder)).AsInt32().NotNullable()
            .WithColumn(nameof(AssemblyMotionType.MinSubmitters)).AsInt32().NotNullable()
            .WithColumn(nameof(AssemblyMotionType.Deadline)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotionType.CompetingDeadline)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotionType.RequiredMajority)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(AssemblyMotionType.WikiTemplate)).AsCustom("MEDIUMTEXT").NotNullable().WithDefaultValue("")
            .WithColumn(nameof(AssemblyMotionType.OpenSlidesCategoryId)).AsInt32().Nullable();

        Create.ForeignKey("FK_AssemblyMotionTypes_PortalId_AssemblyMotionPortals_Id")
            .FromTable(AssemblyMotionType.TableName).ForeignColumn(nameof(AssemblyMotionType.PortalId))
            .ToTable(AssemblyMotionPortal.TableName).PrimaryColumn(nameof(AssemblyMotionPortal.Id));

        Create.Index("IX_AssemblyMotionTypes_Portal_Code")
            .OnTable(AssemblyMotionType.TableName)
            .OnColumn(nameof(AssemblyMotionType.PortalId)).Ascending()
            .OnColumn(nameof(AssemblyMotionType.Code)).Ascending()
            .WithOptions().Unique();

        Create.Table(AssemblyMotion.TableName)
            .WithColumn(nameof(AssemblyMotion.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotion.PortalId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotion.TypeId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotion.Number)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyMotion.Title)).AsString(500).NotNullable()
            .WithColumn(nameof(AssemblyMotion.Section)).AsString(500).Nullable()
            .WithColumn(nameof(AssemblyMotion.TextMarkdown)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotion.TextHtml)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotion.ReasonMarkdown)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotion.ReasonHtml)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotion.Summary)).AsString(2000).Nullable()
            .WithColumn(nameof(AssemblyMotion.Keywords)).AsString(500).Nullable()
            .WithColumn(nameof(AssemblyMotion.PriorDiscussion)).AsString(2000).Nullable()
            .WithColumn(nameof(AssemblyMotion.IsCompetingMotion)).AsBoolean().NotNullable()
            .WithColumn(nameof(AssemblyMotion.Status)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(AssemblyMotion.Result)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(AssemblyMotion.Revision)).AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn(nameof(AssemblyMotion.CheckNote)).AsString(4000).Nullable()
            .WithColumn(nameof(AssemblyMotion.CheckedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.CheckedByUserId)).AsGuid().Nullable()
            .WithColumn(nameof(AssemblyMotion.CreatedByMemberId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotion.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotion.SubmittedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.WithdrawnAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.CollectingReminderSentAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.WikiSyncState)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(AssemblyMotion.WikiSyncedRevision)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyMotion.WikiSyncedHash)).AsString(64).Nullable()
            .WithColumn(nameof(AssemblyMotion.WikiLastSyncedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.WikiLastError)).AsString(4000).Nullable()
            .WithColumn(nameof(AssemblyMotion.WikiFailureCount)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(AssemblyMotion.WikiNextAttemptAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.WikiSyncRequestedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.OpenSlidesMotionId)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyMotion.OpenSlidesPushedRevision)).AsInt32().Nullable()
            .WithColumn(nameof(AssemblyMotion.OpenSlidesPushedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotion.OpenSlidesLastError)).AsString(4000).Nullable()
            .WithColumn(nameof(AssemblyMotion.DeletedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.ForeignKey("FK_AssemblyMotions_PortalId_AssemblyMotionPortals_Id")
            .FromTable(AssemblyMotion.TableName).ForeignColumn(nameof(AssemblyMotion.PortalId))
            .ToTable(AssemblyMotionPortal.TableName).PrimaryColumn(nameof(AssemblyMotionPortal.Id));

        Create.ForeignKey("FK_AssemblyMotions_TypeId_AssemblyMotionTypes_Id")
            .FromTable(AssemblyMotion.TableName).ForeignColumn(nameof(AssemblyMotion.TypeId))
            .ToTable(AssemblyMotionType.TableName).PrimaryColumn(nameof(AssemblyMotionType.Id));

        Create.ForeignKey("FK_AssemblyMotions_CreatedByMemberId_Members_Id")
            .FromTable(AssemblyMotion.TableName).ForeignColumn(nameof(AssemblyMotion.CreatedByMemberId))
            .ToTable(Member.TableName).PrimaryColumn(nameof(Member.Id));

        Create.Index("IX_AssemblyMotions_Portal_Type_Number")
            .OnTable(AssemblyMotion.TableName)
            .OnColumn(nameof(AssemblyMotion.PortalId)).Ascending()
            .OnColumn(nameof(AssemblyMotion.TypeId)).Ascending()
            .OnColumn(nameof(AssemblyMotion.Number)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_AssemblyMotions_Portal_Status")
            .OnTable(AssemblyMotion.TableName)
            .OnColumn(nameof(AssemblyMotion.PortalId)).Ascending()
            .OnColumn(nameof(AssemblyMotion.Status)).Ascending();

        Create.Index("IX_AssemblyMotions_WikiSyncState")
            .OnTable(AssemblyMotion.TableName)
            .OnColumn(nameof(AssemblyMotion.WikiSyncState));

        Create.Table(AssemblyMotionSubmitter.TableName)
            .WithColumn(nameof(AssemblyMotionSubmitter.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionSubmitter.MotionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionSubmitter.MemberId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionSubmitter.IsAuthor)).AsBoolean().NotNullable()
            .WithColumn(nameof(AssemblyMotionSubmitter.JoinedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionSubmitter.JoinedRevision)).AsInt32().NotNullable();

        Create.ForeignKey("FK_AssemblyMotionSubmitters_MotionId_AssemblyMotions_Id")
            .FromTable(AssemblyMotionSubmitter.TableName).ForeignColumn(nameof(AssemblyMotionSubmitter.MotionId))
            .ToTable(AssemblyMotion.TableName).PrimaryColumn(nameof(AssemblyMotion.Id));

        Create.ForeignKey("FK_AssemblyMotionSubmitters_MemberId_Members_Id")
            .FromTable(AssemblyMotionSubmitter.TableName).ForeignColumn(nameof(AssemblyMotionSubmitter.MemberId))
            .ToTable(Member.TableName).PrimaryColumn(nameof(Member.Id));

        Create.Index("IX_AssemblyMotionSubmitters_Motion_Member")
            .OnTable(AssemblyMotionSubmitter.TableName)
            .OnColumn(nameof(AssemblyMotionSubmitter.MotionId)).Ascending()
            .OnColumn(nameof(AssemblyMotionSubmitter.MemberId)).Ascending()
            .WithOptions().Unique();

        Create.Table(AssemblyMotionRevision.TableName)
            .WithColumn(nameof(AssemblyMotionRevision.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionRevision.MotionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.Revision)).AsInt32().NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.Title)).AsString(500).NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.Section)).AsString(500).Nullable()
            .WithColumn(nameof(AssemblyMotionRevision.TextMarkdown)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.ReasonMarkdown)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.Summary)).AsString(2000).Nullable()
            .WithColumn(nameof(AssemblyMotionRevision.Keywords)).AsString(500).Nullable()
            .WithColumn(nameof(AssemblyMotionRevision.PriorDiscussion)).AsString(2000).Nullable()
            .WithColumn(nameof(AssemblyMotionRevision.EditedByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.EditedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.EditedByCommission)).AsBoolean().NotNullable()
            .WithColumn(nameof(AssemblyMotionRevision.ChangeNote)).AsString(2000).Nullable();

        Create.ForeignKey("FK_AssemblyMotionRevisions_MotionId_AssemblyMotions_Id")
            .FromTable(AssemblyMotionRevision.TableName).ForeignColumn(nameof(AssemblyMotionRevision.MotionId))
            .ToTable(AssemblyMotion.TableName).PrimaryColumn(nameof(AssemblyMotion.Id));

        Create.Index("IX_AssemblyMotionRevisions_Motion_Revision")
            .OnTable(AssemblyMotionRevision.TableName)
            .OnColumn(nameof(AssemblyMotionRevision.MotionId)).Ascending()
            .OnColumn(nameof(AssemblyMotionRevision.Revision)).Ascending()
            .WithOptions().Unique();

        Create.Table(AssemblyMotionInquiry.TableName)
            .WithColumn(nameof(AssemblyMotionInquiry.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionInquiry.MotionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionInquiry.QuestionMarkdown)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotionInquiry.QuestionHtml)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(AssemblyMotionInquiry.AskedByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionInquiry.AskedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionInquiry.ResponseDueAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionInquiry.AnswerMarkdown)).AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn(nameof(AssemblyMotionInquiry.AnswerHtml)).AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn(nameof(AssemblyMotionInquiry.AnsweredByMemberId)).AsGuid().Nullable()
            .WithColumn(nameof(AssemblyMotionInquiry.AnsweredAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotionInquiry.ClosedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(AssemblyMotionInquiry.ClosedByUserId)).AsGuid().Nullable();

        Create.ForeignKey("FK_AssemblyMotionInquiries_MotionId_AssemblyMotions_Id")
            .FromTable(AssemblyMotionInquiry.TableName).ForeignColumn(nameof(AssemblyMotionInquiry.MotionId))
            .ToTable(AssemblyMotion.TableName).PrimaryColumn(nameof(AssemblyMotion.Id));

        Create.Table(AssemblyMotionCompetition.TableName)
            .WithColumn(nameof(AssemblyMotionCompetition.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionCompetition.MotionAId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionCompetition.MotionBId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionCompetition.DeclaringMotionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionCompetition.DeclaredByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionCompetition.DeclaredAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(AssemblyMotionCompetition.DeclaredByCommission)).AsBoolean().NotNullable();

        Create.ForeignKey("FK_AssemblyMotionCompetitions_MotionAId_AssemblyMotions_Id")
            .FromTable(AssemblyMotionCompetition.TableName).ForeignColumn(nameof(AssemblyMotionCompetition.MotionAId))
            .ToTable(AssemblyMotion.TableName).PrimaryColumn(nameof(AssemblyMotion.Id));

        Create.ForeignKey("FK_AssemblyMotionCompetitions_MotionBId_AssemblyMotions_Id")
            .FromTable(AssemblyMotionCompetition.TableName).ForeignColumn(nameof(AssemblyMotionCompetition.MotionBId))
            .ToTable(AssemblyMotion.TableName).PrimaryColumn(nameof(AssemblyMotion.Id));

        Create.Index("IX_AssemblyMotionCompetitions_A_B")
            .OnTable(AssemblyMotionCompetition.TableName)
            .OnColumn(nameof(AssemblyMotionCompetition.MotionAId)).Ascending()
            .OnColumn(nameof(AssemblyMotionCompetition.MotionBId)).Ascending()
            .WithOptions().Unique();

        Create.Table(AssemblyMotionUpvote.TableName)
            .WithColumn(nameof(AssemblyMotionUpvote.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(AssemblyMotionUpvote.MotionId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionUpvote.MemberId)).AsGuid().NotNullable()
            .WithColumn(nameof(AssemblyMotionUpvote.CreatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_AssemblyMotionUpvotes_MotionId_AssemblyMotions_Id")
            .FromTable(AssemblyMotionUpvote.TableName).ForeignColumn(nameof(AssemblyMotionUpvote.MotionId))
            .ToTable(AssemblyMotion.TableName).PrimaryColumn(nameof(AssemblyMotion.Id));

        Create.ForeignKey("FK_AssemblyMotionUpvotes_MemberId_Members_Id")
            .FromTable(AssemblyMotionUpvote.TableName).ForeignColumn(nameof(AssemblyMotionUpvote.MemberId))
            .ToTable(Member.TableName).PrimaryColumn(nameof(Member.Id));

        Create.Index("IX_AssemblyMotionUpvotes_Motion_Member")
            .OnTable(AssemblyMotionUpvote.TableName)
            .OnColumn(nameof(AssemblyMotionUpvote.MotionId)).Ascending()
            .OnColumn(nameof(AssemblyMotionUpvote.MemberId)).Ascending()
            .WithOptions().Unique();
    }

    public override void Down() {
        DropTableIfExists(AssemblyMotionUpvote.TableName);
        DropTableIfExists(AssemblyMotionCompetition.TableName);
        DropTableIfExists(AssemblyMotionInquiry.TableName);
        DropTableIfExists(AssemblyMotionRevision.TableName);
        DropTableIfExists(AssemblyMotionSubmitter.TableName);
        DropTableIfExists(AssemblyMotion.TableName);
        DropTableIfExists(AssemblyMotionType.TableName);
        DropTableIfExists(AssemblyMotionPortal.TableName);
    }
}
