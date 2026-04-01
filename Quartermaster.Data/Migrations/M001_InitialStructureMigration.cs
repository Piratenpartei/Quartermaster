using FluentMigrator;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Events;
using Quartermaster.Data.Motions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Email;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Migrations;

[Migration(001)]
public class M001_InitialStructureMigration : MigrationBase {
    public override void Up() {
        Create.Table(User.TableName)
            .WithColumn(nameof(User.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(User.Username)).AsString(64).Unique().Nullable()
            .WithColumn(nameof(User.EMail)).AsString(256)
            .WithColumn(nameof(User.PasswordHash)).AsString(512).Nullable()
            .WithColumn(nameof(User.FirstName)).AsString(256)
            .WithColumn(nameof(User.LastName)).AsString(256)
            .WithColumn(nameof(User.DateOfBirth)).AsDateTime()
            .WithColumn(nameof(User.CitizenshipAdministrativeDivisionId)).AsGuid()
            .WithColumn(nameof(User.PhoneNumber)).AsString(64).Nullable()
            .WithColumn(nameof(User.MembershipFee)).AsDecimal()
            .WithColumn(nameof(User.MemberSince)).AsDateTime()
            .WithColumn(nameof(User.MemberNumber)).AsInt32()
            .WithColumn(nameof(User.AddressStreet)).AsString(256)
            .WithColumn(nameof(User.AddressHouseNbr)).AsString(32)
            .WithColumn(nameof(User.AddressAdministrativeDivisionId)).AsGuid()
            .WithColumn(nameof(User.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(User.DeletedAt)).AsDateTime().Nullable();

        Create.Table(AdministrativeDivision.TableName)
            .WithColumn(nameof(AdministrativeDivision.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(AdministrativeDivision.ParentId)).AsGuid().Nullable()
            .WithColumn(nameof(AdministrativeDivision.Name)).AsString(256)
            .WithColumn(nameof(AdministrativeDivision.Depth)).AsInt32()
            .WithColumn(nameof(AdministrativeDivision.AdminCode)).AsString(256).Nullable()
            .WithColumn(nameof(AdministrativeDivision.PostCodes)).AsString(2048).Nullable();

        Create.ForeignKey("FK_AdministrativeDivisions_ParentId_AdministrativeDivisions_Id")
            .FromTable(AdministrativeDivision.TableName).ForeignColumn(nameof(AdministrativeDivision.ParentId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        Create.ForeignKey("FK_Users_CitizenshipAdminDivId_AdministrativeDivisions_Id")
            .FromTable(User.TableName).ForeignColumn(nameof(User.CitizenshipAdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));
        Create.ForeignKey("FK_Users_AddressAdminDivId_AdministrativeDivisions_Id")
            .FromTable(User.TableName).ForeignColumn(nameof(User.AddressAdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        Create.Table(Chapter.TableName)
            .WithColumn(nameof(Chapter.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(Chapter.Name)).AsString(256)
            .WithColumn(nameof(Chapter.AdministrativeDivisionId)).AsGuid().Nullable()
            .WithColumn(nameof(Chapter.ParentChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(Chapter.ShortCode)).AsString(32).Nullable()
            .WithColumn(nameof(Chapter.ExternalCode)).AsString(128).Nullable();

        Create.ForeignKey("FK_Users_ChapterId_Chapters_Id")
            .FromTable(User.TableName).ForeignColumn(nameof(User.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.ForeignKey("FK_Chapters_AdministrativeDivisionId_AdministrativeDivisions_Id")
            .FromTable(Chapter.TableName).ForeignColumn(nameof(Chapter.AdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        Create.ForeignKey("FK_Chapters_ParentChapterId_Chapters_Id")
            .FromTable(Chapter.TableName).ForeignColumn(nameof(Chapter.ParentChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(Token.TableName)
            .WithColumn(nameof(Token.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(Token.UserId)).AsGuid().Nullable()
            .WithColumn(nameof(Token.Content)).AsString(64).Unique() // SHA256
            .WithColumn(nameof(Token.Type)).AsInt32()
            .WithColumn(nameof(Token.Expires)).AsDateTime().Nullable()
            .WithColumn(nameof(Token.ExtendType)).AsInt32()
            .WithColumn(nameof(Token.SecurityScope)).AsInt32();

        Create.ForeignKey("FK_Tokens_UserId_User_Id")
            .FromTable(Token.TableName).ForeignColumn(nameof(Token.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id))
            .OnDelete(System.Data.Rule.Cascade);

        Create.Table(Permission.TableName)
            .WithColumn(nameof(Permission.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(Permission.Identifier)).AsString(256)
            .WithColumn(nameof(Permission.DisplayName)).AsString(256)
            .WithColumn(nameof(Permission.Global)).AsBoolean();

        Create.Table(UserGlobalPermission.TableName)
            .WithColumn(nameof(UserGlobalPermission.UserId)).AsGuid()
            .WithColumn(nameof(UserGlobalPermission.PermissionId)).AsGuid();

        Create.ForeignKey("FK_UserGlobalPermissions_UserId_User_Id")
            .FromTable(UserGlobalPermission.TableName).ForeignColumn(nameof(UserGlobalPermission.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_UserGlobalPermissions_PermissionId_Permissions_Id")
            .FromTable(UserGlobalPermission.TableName).ForeignColumn(nameof(UserGlobalPermission.PermissionId))
            .ToTable(Permission.TableName).PrimaryColumn(nameof(Permission.Id));

        Create.Table(UserChapterPermission.TableName)
            .WithColumn(nameof(UserChapterPermission.UserId)).AsGuid()
            .WithColumn(nameof(UserChapterPermission.ChapterId)).AsGuid()
            .WithColumn(nameof(UserChapterPermission.PermissionId)).AsGuid();

        Create.ForeignKey("FK_UserChapterPermissions_UserId_User_Id")
            .FromTable(UserChapterPermission.TableName).ForeignColumn(nameof(UserChapterPermission.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_UserChapterPermissions_ChapterId_Chapters_Id")
            .FromTable(UserChapterPermission.TableName).ForeignColumn(nameof(UserChapterPermission.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.ForeignKey("FK_UserChapterPermissions_PermissionId_Permissions_Id")
            .FromTable(UserChapterPermission.TableName).ForeignColumn(nameof(UserChapterPermission.PermissionId))
            .ToTable(Permission.TableName).PrimaryColumn(nameof(Permission.Id));

        Create.Table(ChapterOfficer.TableName)
            .WithColumn(nameof(ChapterOfficer.MemberId)).AsGuid()
            .WithColumn(nameof(ChapterOfficer.ChapterId)).AsGuid()
            .WithColumn(nameof(ChapterOfficer.AssociateType)).AsInt32();

        // FK_ChapterAssociates_MemberId is created after Members table below

        Create.ForeignKey("FK_ChapterAssociates_ChapterId_Chapters_Id")
            .FromTable(ChapterOfficer.TableName).ForeignColumn(nameof(ChapterOfficer.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(Motion.TableName)
            .WithColumn(nameof(Motion.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(Motion.ChapterId)).AsGuid()
            .WithColumn(nameof(Motion.AuthorName)).AsString(256)
            .WithColumn(nameof(Motion.AuthorEMail)).AsString(256)
            .WithColumn(nameof(Motion.Title)).AsString(512)
            .WithColumn(nameof(Motion.Text)).AsString(8192)
            .WithColumn(nameof(Motion.IsPublic)).AsBoolean()
            .WithColumn(nameof(Motion.LinkedMembershipApplicationId)).AsGuid().Nullable()
            .WithColumn(nameof(Motion.LinkedDueSelectionId)).AsGuid().Nullable()
            .WithColumn(nameof(Motion.ApprovalStatus)).AsInt32()
            .WithColumn(nameof(Motion.IsRealized)).AsBoolean()
            .WithColumn(nameof(Motion.CreatedAt)).AsDateTime()
            .WithColumn(nameof(Motion.ResolvedAt)).AsDateTime().Nullable()
            .WithColumn(nameof(Motion.DeletedAt)).AsDateTime().Nullable();

        Create.ForeignKey("FK_Motions_ChapterId_Chapters_Id")
            .FromTable(Motion.TableName).ForeignColumn(nameof(Motion.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(MotionVote.TableName)
            .WithColumn(nameof(MotionVote.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(MotionVote.MotionId)).AsGuid()
            .WithColumn(nameof(MotionVote.UserId)).AsGuid()
            .WithColumn(nameof(MotionVote.Vote)).AsInt32()
            .WithColumn(nameof(MotionVote.VotedAt)).AsDateTime();

        Create.ForeignKey("FK_MotionVotes_MotionId_Motions_Id")
            .FromTable(MotionVote.TableName).ForeignColumn(nameof(MotionVote.MotionId))
            .ToTable(Motion.TableName).PrimaryColumn(nameof(Motion.Id))
            .OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("FK_MotionVotes_UserId_Users_Id")
            .FromTable(MotionVote.TableName).ForeignColumn(nameof(MotionVote.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Table(OptionDefinition.TableName)
            .WithColumn(nameof(OptionDefinition.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(OptionDefinition.Identifier)).AsString(256).Unique()
            .WithColumn(nameof(OptionDefinition.FriendlyName)).AsString(256)
            .WithColumn(nameof(OptionDefinition.DataType)).AsInt32()
            .WithColumn(nameof(OptionDefinition.IsOverridable)).AsBoolean()
            .WithColumn(nameof(OptionDefinition.TemplateModels)).AsString(512);

        Create.Table(SystemOption.TableName)
            .WithColumn(nameof(SystemOption.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(SystemOption.Identifier)).AsString(256)
            .WithColumn(nameof(SystemOption.Value)).AsString(8192)
            .WithColumn(nameof(SystemOption.ChapterId)).AsGuid().Nullable();

        Create.ForeignKey("FK_SystemOptions_ChapterId_Chapters_Id")
            .FromTable(SystemOption.TableName).ForeignColumn(nameof(SystemOption.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id))
            .OnDelete(System.Data.Rule.Cascade);

        Create.Table(DueSelection.TableName)
            .WithColumn(nameof(DueSelection.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(DueSelection.UserId)).AsGuid().Nullable()
            .WithColumn(nameof(DueSelection.FirstName)).AsString()
            .WithColumn(nameof(DueSelection.LastName)).AsString()
            .WithColumn(nameof(DueSelection.EMail)).AsString().Nullable()
            .WithColumn(nameof(DueSelection.MemberNumber)).AsString().Nullable()
            .WithColumn(nameof(DueSelection.SelectedValuation)).AsInt32()
            .WithColumn(nameof(DueSelection.YearlyIncome)).AsDecimal()
            .WithColumn(nameof(DueSelection.MonthlyIncomeGroup)).AsDecimal()
            .WithColumn(nameof(DueSelection.ReducedAmount)).AsDecimal()
            .WithColumn(nameof(DueSelection.SelectedDue)).AsDecimal()
            .WithColumn(nameof(DueSelection.ReducedJustification)).AsString(2048)
            .WithColumn(nameof(DueSelection.ReducedTimeSpan)).AsInt32()
            .WithColumn(nameof(DueSelection.IsDirectDeposit)).AsBoolean()
            .WithColumn(nameof(DueSelection.AccountHolder)).AsString(256)
            .WithColumn(nameof(DueSelection.IBAN)).AsString(64)
            .WithColumn(nameof(DueSelection.PaymentSchedule)).AsInt32()
            .WithColumn(nameof(DueSelection.Status)).AsInt32()
            .WithColumn(nameof(DueSelection.ProcessedByUserId)).AsGuid().Nullable()
            .WithColumn(nameof(DueSelection.ProcessedAt)).AsDateTime().Nullable()
            .WithColumn(nameof(DueSelection.DeletedAt)).AsDateTime().Nullable();

        Create.ForeignKey("FK_DueSelections_UserId_User_Id")
            .FromTable(DueSelection.TableName).ForeignColumn(nameof(DueSelection.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_DueSelections_ProcessedByUserId_Users_Id")
            .FromTable(DueSelection.TableName).ForeignColumn(nameof(DueSelection.ProcessedByUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Table(MembershipApplication.TableName)
            .WithColumn(nameof(MembershipApplication.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(MembershipApplication.FirstName)).AsString(256)
            .WithColumn(nameof(MembershipApplication.LastName)).AsString(256)
            .WithColumn(nameof(MembershipApplication.DateOfBirth)).AsDateTime()
            .WithColumn(nameof(MembershipApplication.Citizenship)).AsString(256)
            .WithColumn(nameof(MembershipApplication.EMail)).AsString(256)
            .WithColumn(nameof(MembershipApplication.PhoneNumber)).AsString(64)
            .WithColumn(nameof(MembershipApplication.AddressStreet)).AsString(256)
            .WithColumn(nameof(MembershipApplication.AddressHouseNbr)).AsString(32)
            .WithColumn(nameof(MembershipApplication.AddressPostCode)).AsString(16)
            .WithColumn(nameof(MembershipApplication.AddressCity)).AsString(256)
            .WithColumn(nameof(MembershipApplication.AddressAdministrativeDivisionId)).AsGuid().Nullable()
            .WithColumn(nameof(MembershipApplication.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(MembershipApplication.DueSelectionId)).AsGuid().Nullable()
            .WithColumn(nameof(MembershipApplication.ConformityDeclarationAccepted)).AsBoolean()
            .WithColumn(nameof(MembershipApplication.HasPriorDeclinedApplication)).AsBoolean()
            .WithColumn(nameof(MembershipApplication.IsMemberOfAnotherParty)).AsBoolean()
            .WithColumn(nameof(MembershipApplication.ApplicationText)).AsString(2048)
            .WithColumn(nameof(MembershipApplication.EntryDate)).AsDateTime()
            .WithColumn(nameof(MembershipApplication.SubmittedAt)).AsDateTime()
            .WithColumn(nameof(MembershipApplication.Status)).AsInt32()
            .WithColumn(nameof(MembershipApplication.ProcessedByUserId)).AsGuid().Nullable()
            .WithColumn(nameof(MembershipApplication.ProcessedAt)).AsDateTime().Nullable()
            .WithColumn(nameof(MembershipApplication.DeletedAt)).AsDateTime().Nullable();

        Create.ForeignKey("FK_MemberApps_AddressAdminDivId_AdminDivs_Id")
            .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.AddressAdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        Create.ForeignKey("FK_MembershipApplications_ChapterId_Chapters_Id")
            .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.ForeignKey("FK_MembershipApplications_DueSelectionId_DueSelections_Id")
            .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.DueSelectionId))
            .ToTable(DueSelection.TableName).PrimaryColumn(nameof(DueSelection.Id));

        Create.ForeignKey("FK_MemberApps_ProcessedByUserId_Users_Id")
            .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.ProcessedByUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Table(Member.TableName)
            .WithColumn(nameof(Member.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(Member.MemberNumber)).AsInt32().Unique()
            .WithColumn(nameof(Member.AdmissionReference)).AsString(64).Nullable()
            .WithColumn(nameof(Member.FirstName)).AsString(256)
            .WithColumn(nameof(Member.LastName)).AsString(256)
            .WithColumn(nameof(Member.Street)).AsString(256).Nullable()
            .WithColumn(nameof(Member.Country)).AsString(16).Nullable()
            .WithColumn(nameof(Member.PostCode)).AsString(16).Nullable()
            .WithColumn(nameof(Member.City)).AsString(256).Nullable()
            .WithColumn(nameof(Member.Phone)).AsString(64).Nullable()
            .WithColumn(nameof(Member.EMail)).AsString(256).Nullable()
            .WithColumn(nameof(Member.DateOfBirth)).AsDateTime().Nullable()
            .WithColumn(nameof(Member.Citizenship)).AsString(64).Nullable()
            .WithColumn(nameof(Member.MembershipFee)).AsDecimal()
            .WithColumn(nameof(Member.ReducedFee)).AsDecimal()
            .WithColumn(nameof(Member.FirstFee)).AsDecimal().Nullable()
            .WithColumn(nameof(Member.OpenFeeTotal)).AsDecimal().Nullable()
            .WithColumn(nameof(Member.ReducedFeeEnd)).AsDateTime().Nullable()
            .WithColumn(nameof(Member.EntryDate)).AsDateTime().Nullable()
            .WithColumn(nameof(Member.ExitDate)).AsDateTime().Nullable()
            .WithColumn(nameof(Member.FederalState)).AsString(16).Nullable()
            .WithColumn(nameof(Member.County)).AsString(256).Nullable()
            .WithColumn(nameof(Member.Municipality)).AsString(256).Nullable()
            .WithColumn(nameof(Member.IsPending)).AsBoolean()
            .WithColumn(nameof(Member.HasVotingRights)).AsBoolean()
            .WithColumn(nameof(Member.ReceivesSurveys)).AsBoolean()
            .WithColumn(nameof(Member.ReceivesActions)).AsBoolean()
            .WithColumn(nameof(Member.ReceivesNewsletter)).AsBoolean()
            .WithColumn(nameof(Member.PostBounce)).AsBoolean()
            .WithColumn(nameof(Member.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(Member.ResidenceAdministrativeDivisionId)).AsGuid().Nullable()
            .WithColumn(nameof(Member.UserId)).AsGuid().Nullable()
            .WithColumn(nameof(Member.LastImportedAt)).AsDateTime();

        Create.ForeignKey("FK_Members_ChapterId_Chapters_Id")
            .FromTable(Member.TableName).ForeignColumn(nameof(Member.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.ForeignKey("FK_Members_ResAdminDivId_AdminDivs_Id")
            .FromTable(Member.TableName).ForeignColumn(nameof(Member.ResidenceAdministrativeDivisionId))
            .ToTable(AdministrativeDivision.TableName).PrimaryColumn(nameof(AdministrativeDivision.Id));

        Create.ForeignKey("FK_Members_UserId_Users_Id")
            .FromTable(Member.TableName).ForeignColumn(nameof(Member.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_ChapterAssociates_MemberId_Members_Id")
            .FromTable(ChapterOfficer.TableName).ForeignColumn(nameof(ChapterOfficer.MemberId))
            .ToTable(Member.TableName).PrimaryColumn(nameof(Member.Id));

        Create.Table(MemberImportLog.TableName)
            .WithColumn(nameof(MemberImportLog.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(MemberImportLog.ImportedAt)).AsDateTime()
            .WithColumn(nameof(MemberImportLog.FileName)).AsString(512)
            .WithColumn(nameof(MemberImportLog.FileHash)).AsString(128)
            .WithColumn(nameof(MemberImportLog.TotalRecords)).AsInt32()
            .WithColumn(nameof(MemberImportLog.NewRecords)).AsInt32()
            .WithColumn(nameof(MemberImportLog.UpdatedRecords)).AsInt32()
            .WithColumn(nameof(MemberImportLog.ErrorCount)).AsInt32()
            .WithColumn(nameof(MemberImportLog.Errors)).AsString(8192).Nullable()
            .WithColumn(nameof(MemberImportLog.DurationMs)).AsInt64();

        Create.Table(EventTemplate.TableName)
            .WithColumn(nameof(EventTemplate.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(EventTemplate.Name)).AsString(512)
            .WithColumn(nameof(EventTemplate.PublicNameTemplate)).AsString(512)
            .WithColumn(nameof(EventTemplate.DescriptionTemplate)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(EventTemplate.Variables)).AsCustom("TEXT")
            .WithColumn(nameof(EventTemplate.ChecklistItemTemplates)).AsCustom("TEXT")
            .WithColumn(nameof(EventTemplate.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(EventTemplate.CreatedAt)).AsDateTime()
            .WithColumn(nameof(EventTemplate.DeletedAt)).AsDateTime().Nullable();

        Create.ForeignKey("FK_EventTemplates_ChapterId_Chapters_Id")
            .FromTable(EventTemplate.TableName).ForeignColumn(nameof(EventTemplate.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(Event.TableName)
            .WithColumn(nameof(Event.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(Event.ChapterId)).AsGuid()
            .WithColumn(nameof(Event.InternalName)).AsString(512)
            .WithColumn(nameof(Event.PublicName)).AsString(512)
            .WithColumn(nameof(Event.Description)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(Event.EventDate)).AsDateTime().Nullable()
            .WithColumn(nameof(Event.IsArchived)).AsBoolean()
            .WithColumn(nameof(Event.EventTemplateId)).AsGuid().Nullable()
            .WithColumn(nameof(Event.CreatedAt)).AsDateTime()
            .WithColumn(nameof(Event.DeletedAt)).AsDateTime().Nullable();

        Create.ForeignKey("FK_Events_ChapterId_Chapters_Id")
            .FromTable(Event.TableName).ForeignColumn(nameof(Event.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.ForeignKey("FK_Events_EventTemplateId_EventTemplates_Id")
            .FromTable(Event.TableName).ForeignColumn(nameof(Event.EventTemplateId))
            .ToTable(EventTemplate.TableName).PrimaryColumn(nameof(EventTemplate.Id));

        Create.Table(EventChecklistItem.TableName)
            .WithColumn(nameof(EventChecklistItem.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(EventChecklistItem.EventId)).AsGuid()
            .WithColumn(nameof(EventChecklistItem.SortOrder)).AsInt32()
            .WithColumn(nameof(EventChecklistItem.ItemType)).AsInt32()
            .WithColumn(nameof(EventChecklistItem.Label)).AsString(1024)
            .WithColumn(nameof(EventChecklistItem.IsCompleted)).AsBoolean()
            .WithColumn(nameof(EventChecklistItem.CompletedAt)).AsDateTime().Nullable()
            .WithColumn(nameof(EventChecklistItem.Configuration)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(EventChecklistItem.ResultId)).AsGuid().Nullable();

        Create.ForeignKey("FK_EventChecklistItems_EventId_Events_Id")
            .FromTable(EventChecklistItem.TableName).ForeignColumn(nameof(EventChecklistItem.EventId))
            .ToTable(Event.TableName).PrimaryColumn(nameof(Event.Id))
            .OnDelete(System.Data.Rule.Cascade);

        Create.Table(EmailLog.TableName)
            .WithColumn(nameof(EmailLog.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(EmailLog.Recipient)).AsString(256)
            .WithColumn(nameof(EmailLog.Subject)).AsString(512)
            .WithColumn(nameof(EmailLog.TemplateIdentifier)).AsString(256).Nullable()
            .WithColumn(nameof(EmailLog.SourceEntityType)).AsString(64).Nullable()
            .WithColumn(nameof(EmailLog.SourceEntityId)).AsGuid().Nullable()
            .WithColumn(nameof(EmailLog.Status)).AsString(32)
            .WithColumn(nameof(EmailLog.Error)).AsCustom("TEXT").Nullable()
            .WithColumn(nameof(EmailLog.AttemptCount)).AsInt32()
            .WithColumn(nameof(EmailLog.CreatedAt)).AsDateTime()
            .WithColumn(nameof(EmailLog.SentAt)).AsDateTime().Nullable();

        Create.Table(AuditLog.AuditLog.TableName)
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("EntityType").AsString(64)
            .WithColumn("EntityId").AsGuid()
            .WithColumn("Action").AsString(64)
            .WithColumn("FieldName").AsString(256).Nullable()
            .WithColumn("OldValue").AsCustom("TEXT").Nullable()
            .WithColumn("NewValue").AsCustom("TEXT").Nullable()
            .WithColumn("UserId").AsGuid().Nullable()
            .WithColumn("UserDisplayName").AsString(256).Nullable()
            .WithColumn("Timestamp").AsDateTime();

        // Secondary indexes
        Create.Index("IX_Members_LastName_FirstName").OnTable(Member.TableName)
            .OnColumn("LastName").Ascending().OnColumn("FirstName").Ascending();
        Create.Index("IX_Events_ChapterId").OnTable(Event.TableName).OnColumn("ChapterId").Ascending();
        Create.Index("IX_Motions_ChapterId").OnTable(Motion.TableName).OnColumn("ChapterId").Ascending();
        Create.Index("IX_Chapters_ShortCode").OnTable(Chapter.TableName).OnColumn("ShortCode").Ascending();
        Create.Index("IX_Chapters_ExternalCode").OnTable(Chapter.TableName).OnColumn("ExternalCode").Ascending();
        Create.Index("IX_EventChecklistItems_EventId").OnTable(EventChecklistItem.TableName).OnColumn("EventId").Ascending();
        Create.Index("IX_MotionVotes_MotionId").OnTable(MotionVote.TableName).OnColumn("MotionId").Ascending();
        Create.Index("IX_ChapterAssociates_ChapterId").OnTable(ChapterOfficer.TableName).OnColumn("ChapterId").Ascending();
        Create.Index("IX_Tokens_UserId").OnTable(Token.TableName).OnColumn("UserId").Ascending();
        Create.Index("IX_EmailLogs_SourceEntityType_SourceEntityId").OnTable(EmailLog.TableName)
            .OnColumn(nameof(EmailLog.SourceEntityType)).Ascending().OnColumn(nameof(EmailLog.SourceEntityId)).Ascending();
        Create.Index("IX_EmailLogs_Status").OnTable(EmailLog.TableName)
            .OnColumn(nameof(EmailLog.Status)).Ascending();
        Create.Index("IX_AuditLogs_EntityType_EntityId").OnTable(AuditLog.AuditLog.TableName)
            .OnColumn("EntityType").Ascending().OnColumn("EntityId").Ascending();
    }

    public override void Down() {
        DisableForeignKeyChecks();

        DropTableIfExists(EventChecklistItem.TableName);
        DropTableIfExists(Event.TableName);
        DropTableIfExists(EventTemplate.TableName);
        DropTableIfExists(MemberImportLog.TableName);
        DropTableIfExists(Member.TableName);
        DropTableIfExists(SystemOption.TableName);
        DropTableIfExists(OptionDefinition.TableName);
        DropTableIfExists(MotionVote.TableName);
        DropTableIfExists(Motion.TableName);
        DropTableIfExists(MembershipApplication.TableName);
        DropTableIfExists(DueSelection.TableName);
        DropTableIfExists(ChapterOfficer.TableName);
        DropTableIfExists(UserGlobalPermission.TableName);
        DropTableIfExists(UserChapterPermission.TableName);
        DropTableIfExists(Token.TableName);
        DropTableIfExists(User.TableName);
        DropTableIfExists(Chapter.TableName);
        DropTableIfExists(AdministrativeDivision.TableName);
        DropTableIfExists(Permission.TableName);

        DropTableIfExists(EmailLog.TableName);
        DropTableIfExists(AuditLog.AuditLog.TableName);

        EnableForeignKeyChecks();
    }
}
