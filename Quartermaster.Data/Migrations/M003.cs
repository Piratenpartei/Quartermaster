using FluentMigrator;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.PdfTemplates;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Migrations;

[Migration(005)]
public class M003 : MigrationBase {
    public override void Up() {
        Create.Table(ExpenseReimbursement.TableName)
            .WithColumn(nameof(ExpenseReimbursement.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ExpenseReimbursement.OwnerUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(ExpenseReimbursement.Status)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(ExpenseReimbursement.FirstName)).AsString(128).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.LastName)).AsString(128).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.MemberNumber)).AsInt32().Nullable()
            .WithColumn(nameof(ExpenseReimbursement.Street)).AsString(256).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.PostCode)).AsString(16).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.City)).AsString(128).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ExpenseReimbursement.ChapterNameSnapshot)).AsString(256).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.Function)).AsString(128).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.MotionDate)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ExpenseReimbursement.DonatedAmount)).AsCustom("DECIMAL(12,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(ExpenseReimbursement.PayoutAmount)).AsCustom("DECIMAL(12,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(ExpenseReimbursement.PayoutMethod)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(ExpenseReimbursement.RequestsDonationReceipt)).AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn(nameof(ExpenseReimbursement.Iban)).AsString(34).Nullable()
            .WithColumn(nameof(ExpenseReimbursement.SignatureLocation)).AsString(128).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursement.FormDate)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ExpenseReimbursement.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ExpenseReimbursement.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ExpenseReimbursement.SubmittedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.ForeignKey("FK_ExpenseReimbursements_OwnerUserId_Users_Id")
            .FromTable(ExpenseReimbursement.TableName).ForeignColumn(nameof(ExpenseReimbursement.OwnerUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_ExpenseReimbursements_ChapterId_Chapters_Id")
            .FromTable(ExpenseReimbursement.TableName).ForeignColumn(nameof(ExpenseReimbursement.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Index("IX_ExpenseReimbursements_OwnerUserId")
            .OnTable(ExpenseReimbursement.TableName)
            .OnColumn(nameof(ExpenseReimbursement.OwnerUserId));

        Create.Table(ExpenseReimbursementLineItem.TableName)
            .WithColumn(nameof(ExpenseReimbursementLineItem.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ExpenseReimbursementLineItem.ExpenseReimbursementId)).AsGuid().NotNullable()
            .WithColumn(nameof(ExpenseReimbursementLineItem.SortOrder)).AsInt32().NotNullable()
            .WithColumn(nameof(ExpenseReimbursementLineItem.Description)).AsString(512).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(ExpenseReimbursementLineItem.Value)).AsCustom("DECIMAL(12,2)").NotNullable();

        Create.ForeignKey("FK_ExpenseReimbursementLineItems_ExpenseReimbursementId")
            .FromTable(ExpenseReimbursementLineItem.TableName).ForeignColumn(nameof(ExpenseReimbursementLineItem.ExpenseReimbursementId))
            .ToTable(ExpenseReimbursement.TableName).PrimaryColumn(nameof(ExpenseReimbursement.Id));

        Create.Index("IX_ExpenseReimbursementLineItems_FormId")
            .OnTable(ExpenseReimbursementLineItem.TableName)
            .OnColumn(nameof(ExpenseReimbursementLineItem.ExpenseReimbursementId));

        Create.Table(PdfTemplate.TableName)
            .WithColumn(nameof(PdfTemplate.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(PdfTemplate.Key)).AsString(128).NotNullable()
            .WithColumn(nameof(PdfTemplate.Body)).AsCustom("LONGTEXT").NotNullable()
            .WithColumn(nameof(PdfTemplate.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(PdfTemplate.UpdatedByUserId)).AsGuid().Nullable();

        Create.Index("IX_PdfTemplates_Key")
            .OnTable(PdfTemplate.TableName)
            .OnColumn(nameof(PdfTemplate.Key)).Unique();

        Create.Table(ProgrammeSource.TableName)
            .WithColumn(nameof(ProgrammeSource.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ProgrammeSource.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(ProgrammeSource.WikiPageTitle)).AsString(256).NotNullable()
            .WithColumn(nameof(ProgrammeSource.LastSyncedAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(ProgrammeSource.LastRevisionId)).AsInt64().Nullable()
            .WithColumn(nameof(ProgrammeSource.LastError)).AsCustom("TEXT").Nullable();

        Create.Index("IX_ProgrammeSources_Chapter_Title")
            .OnTable(ProgrammeSource.TableName)
            .OnColumn(nameof(ProgrammeSource.ChapterId)).Ascending()
            .OnColumn(nameof(ProgrammeSource.WikiPageTitle)).Ascending()
            .WithOptions().Unique();

        Create.Table(ProgrammeSection.TableName)
            .WithColumn(nameof(ProgrammeSection.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ProgrammeSection.SourceId)).AsGuid().NotNullable()
            .WithColumn(nameof(ProgrammeSection.PageTitle)).AsString(256).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Heading)).AsString(512).NotNullable()
            .WithColumn(nameof(ProgrammeSection.HeadingPath)).AsString(1024).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Anchor)).AsString(512).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Body)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(ProgrammeSection.ContentHash)).AsString(64).NotNullable()
            .WithColumn(nameof(ProgrammeSection.Embedding)).AsCustom("BLOB").NotNullable()
            .WithColumn(nameof(ProgrammeSection.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_ProgrammeSections_SourceId")
            .FromTable(ProgrammeSection.TableName).ForeignColumn(nameof(ProgrammeSection.SourceId))
            .ToTable(ProgrammeSource.TableName).PrimaryColumn(nameof(ProgrammeSource.Id));

        Create.Table(NewsFeed.TableName)
            .WithColumn(nameof(NewsFeed.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(NewsFeed.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(NewsFeed.Name)).AsString(256).NotNullable()
            .WithColumn(nameof(NewsFeed.Url)).AsString(1024).NotNullable()
            .WithColumn(nameof(NewsFeed.Kind)).AsInt32().NotNullable()
            .WithColumn(nameof(NewsFeed.IsEnabled)).AsBoolean().NotNullable()
            .WithColumn(nameof(NewsFeed.LastPolledAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(NewsFeed.LastError)).AsCustom("TEXT").Nullable();

        Create.Table(NewsItem.TableName)
            .WithColumn(nameof(NewsItem.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(NewsItem.FeedId)).AsGuid().NotNullable()
            .WithColumn(nameof(NewsItem.ExternalId)).AsString(512).NotNullable()
            .WithColumn(nameof(NewsItem.Url)).AsString(1024).NotNullable()
            .WithColumn(nameof(NewsItem.Title)).AsString(1024).NotNullable()
            .WithColumn(nameof(NewsItem.Teaser)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(NewsItem.ArticleText)).AsCustom("MEDIUMTEXT").Nullable()
            .WithColumn(nameof(NewsItem.HasFullText)).AsBoolean().NotNullable()
            .WithColumn(nameof(NewsItem.PublishedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(NewsItem.FetchedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(NewsItem.Status)).AsInt32().NotNullable()
            .WithColumn(nameof(NewsItem.TriageReason)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(NewsItem.BestSimilarity)).AsDouble().Nullable()
            .WithColumn(nameof(NewsItem.MatchedSectionIds)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(NewsItem.FailureCount)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(NewsItem.LastError)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(NewsItem.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_NewsItems_FeedId")
            .FromTable(NewsItem.TableName).ForeignColumn(nameof(NewsItem.FeedId))
            .ToTable(NewsFeed.TableName).PrimaryColumn(nameof(NewsFeed.Id));

        Create.Index("IX_NewsItems_Feed_ExternalId")
            .OnTable(NewsItem.TableName)
            .OnColumn(nameof(NewsItem.FeedId)).Ascending()
            .OnColumn(nameof(NewsItem.ExternalId)).Ascending()
            .WithOptions().Unique();

        Create.Index("IX_NewsItems_Status")
            .OnTable(NewsItem.TableName)
            .OnColumn(nameof(NewsItem.Status));

        Create.Table(PostCandidate.TableName)
            .WithColumn(nameof(PostCandidate.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(PostCandidate.NewsItemId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostCandidate.Text)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostCandidate.SectionId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostCandidate.SortOrder)).AsInt32().NotNullable()
            .WithColumn(nameof(PostCandidate.CreatedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_PostCandidates_NewsItemId")
            .FromTable(PostCandidate.TableName).ForeignColumn(nameof(PostCandidate.NewsItemId))
            .ToTable(NewsItem.TableName).PrimaryColumn(nameof(NewsItem.Id));

        Create.Table(PostPublication.TableName)
            .WithColumn(nameof(PostPublication.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(PostPublication.NewsItemId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostPublication.CandidateId)).AsGuid().Nullable()
            .WithColumn(nameof(PostPublication.FinalText)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostPublication.ChannelIds)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostPublication.PostizPostIds)).AsCustom("TEXT").NotNullable()
            .WithColumn(nameof(PostPublication.ScheduledFor)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(PostPublication.Status)).AsInt32().NotNullable()
            .WithColumn(nameof(PostPublication.Error)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(PostPublication.ApprovedByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(PostPublication.ApprovedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_PostPublications_NewsItemId")
            .FromTable(PostPublication.TableName).ForeignColumn(nameof(PostPublication.NewsItemId))
            .ToTable(NewsItem.TableName).PrimaryColumn(nameof(NewsItem.Id));
    }

    public override void Down() {
        DropTableIfExists(PostPublication.TableName);
        DropTableIfExists(PostCandidate.TableName);
        DropTableIfExists(NewsItem.TableName);
        DropTableIfExists(NewsFeed.TableName);
        DropTableIfExists(ProgrammeSection.TableName);
        DropTableIfExists(ProgrammeSource.TableName);
        DropTableIfExists(PdfTemplate.TableName);
        DropTableIfExists(ExpenseReimbursementLineItem.TableName);
        DropTableIfExists(ExpenseReimbursement.TableName);
    }
}
