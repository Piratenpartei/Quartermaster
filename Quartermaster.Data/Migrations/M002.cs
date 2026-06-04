using FluentMigrator;
using Quartermaster.Data.ChapterMails;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Templates;
using Quartermaster.Data.Users;

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

        Create.Table(ChapterMail.TableName)
            .WithColumn(nameof(ChapterMail.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(ChapterMail.ChapterId)).AsGuid().NotNullable()
            .WithColumn(nameof(ChapterMail.Title)).AsString(256).NotNullable()
            .WithColumn(nameof(ChapterMail.Subject)).AsString(512).Nullable()
            .WithColumn(nameof(ChapterMail.TargetType)).AsInt32().NotNullable()
            .WithColumn(nameof(ChapterMail.TargetId)).AsGuid().NotNullable()
            .WithColumn(nameof(ChapterMail.IncludeDescendants)).AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn(nameof(ChapterMail.TemplateId)).AsGuid().Nullable()
            .WithColumn(nameof(ChapterMail.Content)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(ChapterMail.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(ChapterMail.CreatedByUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(ChapterMail.SentAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(ChapterMail.SentByUserId)).AsGuid().Nullable()
            .WithColumn(nameof(ChapterMail.RecipientCount)).AsInt32().Nullable()
            .WithColumn(nameof(ChapterMail.DeletedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.ForeignKey("FK_ChapterMails_ChapterId_Chapters_Id")
            .FromTable(ChapterMail.TableName).ForeignColumn(nameof(ChapterMail.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.ForeignKey("FK_ChapterMails_TemplateId_Templates_Id")
            .FromTable(ChapterMail.TableName).ForeignColumn(nameof(ChapterMail.TemplateId))
            .ToTable(Template.TableName).PrimaryColumn(nameof(Template.Id));

        Create.ForeignKey("FK_ChapterMails_CreatedByUserId_Users_Id")
            .FromTable(ChapterMail.TableName).ForeignColumn(nameof(ChapterMail.CreatedByUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_ChapterMails_SentByUserId_Users_Id")
            .FromTable(ChapterMail.TableName).ForeignColumn(nameof(ChapterMail.SentByUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Index("IX_ChapterMails_ChapterId")
            .OnTable(ChapterMail.TableName)
            .OnColumn(nameof(ChapterMail.ChapterId));
    }

    public override void Down() {
        DropTableIfExists(ChapterMail.TableName);

        Delete.Column(nameof(MemberImportLog.PromotedRecords)).FromTable(MemberImportLog.TableName);
        Delete.Column(nameof(MemberImportLog.UpdatedFloatingRecords)).FromTable(MemberImportLog.TableName);
        Delete.Column(nameof(MemberImportLog.NewFloatingRecords)).FromTable(MemberImportLog.TableName);
        Delete.Column(nameof(MemberImportLog.SkippedRecords)).FromTable(MemberImportLog.TableName);

        Alter.Column(nameof(Member.MemberNumber))
            .OnTable(Member.TableName)
            .AsInt32().NotNullable();
    }
}
