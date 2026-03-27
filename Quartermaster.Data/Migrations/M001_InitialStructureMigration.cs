using FluentMigrator;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Tokens;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Migrations;

[Migration(001)]
public class M001_InitialStructureMigration : Migration {
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
            .WithColumn(nameof(User.ChapterId)).AsGuid().Nullable();

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
            .WithColumn(nameof(Chapter.Name)).AsString(256);

        Create.ForeignKey("FK_Users_ChapterId_Chapters_Id")
            .FromTable(User.TableName).ForeignColumn(nameof(User.ChapterId))
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
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Table(MembershipApplication.TableName)
            .WithColumn(nameof(MembershipApplication.Id)).AsGuid().PrimaryKey().Indexed()
            .WithColumn(nameof(MembershipApplication.UserId)).AsGuid()
            .WithColumn(nameof(MembershipApplication.HasPriorDeclinedApplication)).AsBoolean()
            .WithColumn(nameof(MembershipApplication.IsMemberOfAnotherParty)).AsBoolean()
            .WithColumn(nameof(MembershipApplication.ApplicationText)).AsString(2048);

        Create.ForeignKey("FK_MembershipApplications_UserId_Users_Id")
            .FromTable(MembershipApplication.TableName).ForeignColumn(nameof(MembershipApplication.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

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
            .WithColumn(nameof(ChapterOfficer.UserId)).AsGuid()
            .WithColumn(nameof(ChapterOfficer.ChapterId)).AsGuid()
            .WithColumn(nameof(ChapterOfficer.AssociateType)).AsInt32();

        Create.ForeignKey("FK_ChapterAssociates_UserId_User_Id")
            .FromTable(ChapterOfficer.TableName).ForeignColumn(nameof(ChapterOfficer.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.ForeignKey("FK_ChapterAssociates_ChapterId_Chapters_Id")
            .FromTable(ChapterOfficer.TableName).ForeignColumn(nameof(ChapterOfficer.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Table(DueSelection.TableName)
            .WithColumn(nameof(DueSelection.Id)).AsGuid()
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
            .WithColumn(nameof(DueSelection.PaymentSchedule)).AsInt32();

        Create.ForeignKey("FK_DueSelections_UserId_User_Id")
            .FromTable(DueSelection.TableName).ForeignColumn(nameof(DueSelection.UserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));
    }

    public override void Down() {
        Delete.ForeignKey("FK_DueSelections_UserId_User_Id")
            .OnTable(DueSelection.TableName);

        Delete.ForeignKey("FK_AdministrativeDivisions_ParentId_AdministrativeDivisions_Id")
            .OnTable(AdministrativeDivision.TableName);

        Delete.ForeignKey("FK_Users_CitizenshipAdminDivId_AdministrativeDivisions_Id")
            .OnTable(User.TableName);
        Delete.ForeignKey("FK_Users_AddressAdminDivId_AdministrativeDivisions_Id")
            .OnTable(User.TableName);

        Delete.ForeignKey("FK_Users_ChapterId_Chapters_Id")
            .OnTable(User.TableName);
        Delete.ForeignKey("FK_MembershipApplications_UserId_Users_Id")
            .OnTable(MembershipApplication.TableName);

        Delete.ForeignKey("FK_UserGlobalPermissions_UserId_User_Id")
            .OnTable(UserGlobalPermission.TableName);
        Delete.ForeignKey("FK_UserGlobalPermissions_PermissionId_Permissions_Id")
            .OnTable(UserGlobalPermission.TableName);

        Delete.ForeignKey("FK_UserChapterPermissions_UserId_User_Id")
            .OnTable(UserChapterPermission.TableName);
        Delete.ForeignKey("FK_UserChapterPermissions_ChapterId_Chapters_Id")
            .OnTable(UserChapterPermission.TableName);
        Delete.ForeignKey("FK_UserChapterPermissions_PermissionId_Permissions_Id")
            .OnTable(UserChapterPermission.TableName);

        Delete.ForeignKey("FK_ChapterAssociates_UserId_User_Id")
            .OnTable(ChapterOfficer.TableName);
        Delete.ForeignKey("FK_ChapterAssociates_ChapterId_Chapters_Id")
            .OnTable(ChapterOfficer.TableName);

        Delete.ForeignKey("FK_Tokens_UserId_User_Id")
            .OnTable(Token.TableName);

        Delete.Table(User.TableName);
        Delete.Table(AdministrativeDivision.TableName);
        Delete.Table(Chapter.TableName);
        Delete.Table(Token.TableName);
        Delete.Table(MembershipApplication.TableName);
        Delete.Table(Permission.TableName);
        Delete.Table(UserGlobalPermission.TableName);
        Delete.Table(UserChapterPermission.TableName);
        Delete.Table(ChapterOfficer.TableName);
        Delete.Table(DueSelection.TableName);
    }
}
