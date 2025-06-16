using FluentMigrator;

namespace Quartermaster.Data.Migrations;

[Migration(0)]
public class Migrations_0 : Migration {
    public override void Up() {
        Create.Table("Users")
            .WithColumn("Id").AsGuid().PrimaryKey().Indexed()
            .WithColumn("Username").AsString(64).Unique().Nullable()
            .WithColumn("EMail").AsString(256)
            .WithColumn("PasswordHash").AsString(512).Nullable()
            .WithColumn("FirstName").AsString(256)
            .WithColumn("LastName").AsString(256)
            .WithColumn("DateOfBirth").AsDateTime()
            .WithColumn("CitizenshipAdministrativeDivisionId").AsGuid()
            .WithColumn("PhoneNumber").AsString(64).Nullable()
            .WithColumn("MembershipFee").AsDecimal()
            .WithColumn("MemberSince").AsDateTime()
            .WithColumn("MemberNumber").AsInt32()
            .WithColumn("AddressStreet").AsString(256)
            .WithColumn("AddressHouseNbr").AsString(32)
            .WithColumn("AddressAdministrativeDivisionId").AsGuid()
            .WithColumn("ChapterId").AsGuid().Nullable();

        Create.Table("AdministrativeDivisions")
            .WithColumn("Id").AsGuid().PrimaryKey().Indexed()
            .WithColumn("ParentId").AsGuid().Nullable()
            .WithColumn("Name").AsString(256)
            .WithColumn("Depth").AsInt32()
            .WithColumn("AdminCode").AsInt32().Nullable()
            .WithColumn("PostCode").AsString(256).Nullable();

        Create.ForeignKey("FK_AdministrativeDivisions_ParentId_AdministrativeDivisions_Id")
            .FromTable("AdministrativeDivisions").ForeignColumn("ParentId")
            .ToTable("AdministrativeDivisions").PrimaryColumn("Id");

        Create.ForeignKey("FK_Users_CitizenshipAdminDivId_AdministrativeDivisions_Id")
            .FromTable("Users").ForeignColumn("CitizenshipAdministrativeDivisionId")
            .ToTable("AdministrativeDivisions").PrimaryColumn("Id");
        Create.ForeignKey("FK_Users_AddressAdminDivId_AdministrativeDivisions_Id")
            .FromTable("Users").ForeignColumn("AddressAdministrativeDivisionId")
            .ToTable("AdministrativeDivisions").PrimaryColumn("Id");

        Create.Table("Chapters")
            .WithColumn("Id").AsGuid().PrimaryKey().Indexed()
            .WithColumn("Name").AsString(256);

        Create.ForeignKey("FK_Users_ChapterId_Chapters_Id")
            .FromTable("Users").ForeignColumn("ChapterId")
            .ToTable("Chapters").PrimaryColumn("Id");

        Create.Table("Tokens")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsGuid().Nullable()
            .WithColumn("Content").AsString(64).Unique() // SHA256
            .WithColumn("Type").AsInt32()
            .WithColumn("Expires").AsDateTime().Nullable()
            .WithColumn("ExtendType").AsInt32()
            .WithColumn("SecurityScope").AsInt32();

        Create.ForeignKey("FK_Tokens_UserId_User_Id")
            .FromTable("Tokens").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.Table("MembershipApplications")
            .WithColumn("Id").AsGuid().PrimaryKey().Indexed()
            .WithColumn("UserId").AsGuid()
            .WithColumn("HasPriorDeclinedApplication").AsBoolean()
            .WithColumn("IsMemberOfAnotherParty").AsBoolean()
            .WithColumn("ApplicationText").AsString(2048);

        Create.ForeignKey("FK_MembershipApplications_UserId_Users_Id")
            .FromTable("MembershipApplications").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.Table("Permissions")
            .WithColumn("Id").AsGuid().PrimaryKey().Indexed()
            .WithColumn("Identifier").AsString(256)
            .WithColumn("DisplayName").AsString(256)
            .WithColumn("Global").AsBoolean();

        Create.Table("UserGlobalPermissions")
            .WithColumn("UserId").AsGuid()
            .WithColumn("PermissionId").AsGuid();

        Create.ForeignKey("FK_UserGlobalPermissions_UserId_User_Id")
            .FromTable("UserGlobalPermissions").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_UserGlobalPermissions_PermissionId_Permissions_Id")
            .FromTable("UserGlobalPermissions").ForeignColumn("PermissionId")
            .ToTable("Permissions").PrimaryColumn("Id");

        Create.Table("UserChapterPermissions")
            .WithColumn("UserId").AsGuid()
            .WithColumn("ChapterId").AsGuid()
            .WithColumn("PermissionId").AsGuid();

        Create.ForeignKey("FK_UserChapterPermissions_UserId_User_Id")
            .FromTable("UserChapterPermissions").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_UserChapterPermissions_ChapterId_Chapters_Id")
            .FromTable("UserChapterPermissions").ForeignColumn("ChapterId")
            .ToTable("Chapters").PrimaryColumn("Id");

        Create.ForeignKey("FK_UserChapterPermissions_PermissionId_Permissions_Id")
            .FromTable("UserChapterPermissions").ForeignColumn("PermissionId")
            .ToTable("Permissions").PrimaryColumn("Id");

        Create.Table("ChapterAssociates")
            .WithColumn("UserId").AsGuid()
            .WithColumn("ChapterId").AsGuid()
            .WithColumn("AssociateType").AsInt32();

        Create.ForeignKey("FK_ChapterAssociates_UserId_User_Id")
            .FromTable("ChapterAssociates").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");

        Create.ForeignKey("FK_ChapterAssociates_ChapterId_Chapters_Id")
            .FromTable("ChapterAssociates").ForeignColumn("ChapterId")
            .ToTable("Chapters").PrimaryColumn("Id");

        Create.Table("DueSelections")
            .WithColumn("Id").AsGuid()
            .WithColumn("UserId").AsGuid().Nullable()
            .WithColumn("FirstName").AsString()
            .WithColumn("LastName").AsString()
            .WithColumn("EMail").AsString().Nullable()
            .WithColumn("MemberNumber").AsString().Nullable()
            .WithColumn("SelectedValuation").AsInt32()
            .WithColumn("YearlyIncome").AsDecimal()
            .WithColumn("MonthlyIncomeGroup").AsDecimal()
            .WithColumn("ReducedAmount").AsDecimal()
            .WithColumn("SelectedDue").AsDecimal()
            .WithColumn("ReducedJustification").AsString(2048)
            .WithColumn("ReducedTimeSpan").AsInt32()
            .WithColumn("IsDirectDeposit").AsBoolean()
            .WithColumn("AccountHolder").AsString(256)
            .WithColumn("IBAN").AsString(64)
            .WithColumn("PaymentSchedule").AsInt32();

        Create.ForeignKey("FK_DueSelections_UserId_User_Id")
            .FromTable("DueSelections").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");
    }

    public override void Down() {
        Delete.ForeignKey("FK_DueSelections_UserId_User_Id");

        Delete.ForeignKey("FK_AdministrativeDivisions_ParentId_AdministrativeDivisions_Id");

        Delete.ForeignKey("FK_Users_CitizenshipAdminDivId_AdministrativeDivisions_Id");
        Delete.ForeignKey("FK_Users_AddressAdminDivId_AdministrativeDivisions_Id");

        Delete.ForeignKey("FK_Users_ChapterId_Chapters_Id");
        Delete.ForeignKey("FK_MembershipApplications_UserId_Users_Id");

        Delete.ForeignKey("FK_UserGlobalPermissions_UserId_User_Id");
        Delete.ForeignKey("FK_UserGlobalPermissions_PermissionId_Permissions_Id");

        Delete.ForeignKey("FK_UserChapterPermissions_UserId_User_Id");
        Delete.ForeignKey("FK_UserChapterPermissions_ChapterId_Chapters_Id");
        Delete.ForeignKey("FK_UserChapterPermissions_PermissionId_Permissions_Id");

        Delete.ForeignKey("FK_ChapterAssociates_UserId_User_Id");
        Delete.ForeignKey("FK_ChapterAssociates_ChapterId_Chapters_Id");

        Delete.ForeignKey("FK_Tokens_UserId_User_Id");

        Delete.Table("Users");
        Delete.Table("AdministrativeDivisions");
        Delete.Table("Chapters");
        Delete.Table("Tokens");
        Delete.Table("MembershipApplications");
        Delete.Table("Permissions");
        Delete.Table("UserGlobalPermissions");
        Delete.Table("UserChapterPermissions");
        Delete.Table("ChapterAssociates");
        Delete.Table("DueSelections");
    }
}