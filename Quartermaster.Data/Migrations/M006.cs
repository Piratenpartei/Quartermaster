using FluentMigrator;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Receipts;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Data.Users;

namespace Quartermaster.Data.Migrations;

[Migration(008)]
public class M006 : MigrationBase {
    public override void Up() {
        Create.Table(TravelExpenseRuleSet.TableName)
            .WithColumn(nameof(TravelExpenseRuleSet.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(TravelExpenseRuleSet.ChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(TravelExpenseRuleSet.ValidFrom)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(TravelExpenseRuleSet.VersionLabel)).AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(TravelExpenseRuleSet.HeaderLines)).AsString(1024).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(TravelExpenseRuleSet.RulesText)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(TravelExpenseRuleSet.TemplateKey)).AsString(128).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(TravelExpenseRuleSet.CarRatesJson)).AsString(2048).NotNullable().WithDefaultValue("[]")
            .WithColumn(nameof(TravelExpenseRuleSet.MotorcycleRatesJson)).AsString(2048).NotNullable().WithDefaultValue("[]")
            .WithColumn(nameof(TravelExpenseRuleSet.MealDayTripRate)).AsCustom("DECIMAL(8,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.MealArrivalDepartureRate)).AsCustom("DECIMAL(8,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.MealFullDayRate)).AsCustom("DECIMAL(8,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.BreakfastReductionPercent)).AsCustom("DECIMAL(5,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.LunchReductionPercent)).AsCustom("DECIMAL(5,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.DinnerReductionPercent)).AsCustom("DECIMAL(5,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.AccommodationFlatRate)).AsCustom("DECIMAL(8,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.SubmissionDeadlineDays)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.CutoffDay)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.CutoffMonth)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseRuleSet.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(TravelExpenseRuleSet.UpdatedByUserId)).AsGuid().Nullable();

        Create.ForeignKey("FK_TravelExpenseRuleSets_ChapterId_Chapters_Id")
            .FromTable(TravelExpenseRuleSet.TableName).ForeignColumn(nameof(TravelExpenseRuleSet.ChapterId))
            .ToTable(Chapter.TableName).PrimaryColumn(nameof(Chapter.Id));

        Create.Index("IX_TravelExpenseRuleSets_ChapterId_ValidFrom")
            .OnTable(TravelExpenseRuleSet.TableName)
            .OnColumn(nameof(TravelExpenseRuleSet.ChapterId)).Ascending()
            .OnColumn(nameof(TravelExpenseRuleSet.ValidFrom)).Ascending();

        Create.Table(UserReceipt.TableName)
            .WithColumn(nameof(UserReceipt.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(UserReceipt.OwnerUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(UserReceipt.Label)).AsString(UserReceipt.LabelMaxLength).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(UserReceipt.FileName)).AsString(UserReceipt.FileNameMaxLength).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(UserReceipt.ContentType)).AsString(64).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(UserReceipt.SizeBytes)).AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(UserReceipt.StoredName)).AsString(64).NotNullable()
            .WithColumn(nameof(UserReceipt.UploadedAt)).AsCustom("DATETIME(6)").NotNullable();

        Create.ForeignKey("FK_UserReceipts_OwnerUserId_Users_Id")
            .FromTable(UserReceipt.TableName).ForeignColumn(nameof(UserReceipt.OwnerUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Index("IX_UserReceipts_OwnerUserId")
            .OnTable(UserReceipt.TableName)
            .OnColumn(nameof(UserReceipt.OwnerUserId));

        Create.Table(TravelExpenseReimbursement.TableName)
            .WithColumn(nameof(TravelExpenseReimbursement.Id)).AsGuid().PrimaryKey()
            .WithColumn(nameof(TravelExpenseReimbursement.OwnerUserId)).AsGuid().NotNullable()
            .WithColumn(nameof(TravelExpenseReimbursement.Status)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseReimbursement.TravelChapterId)).AsGuid().Nullable()
            .WithColumn(nameof(TravelExpenseReimbursement.ChapterName)).AsString(256).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(TravelExpenseReimbursement.Purpose)).AsString(256).NotNullable().WithDefaultValue("")
            .WithColumn(nameof(TravelExpenseReimbursement.StartAt)).AsCustom("DATETIME(6)").Nullable()
            .WithColumn(nameof(TravelExpenseReimbursement.Total)).AsCustom("DECIMAL(12,2)").NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(TravelExpenseReimbursement.InputJson)).AsCustom("MEDIUMTEXT").NotNullable()
            .WithColumn(nameof(TravelExpenseReimbursement.CreatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(TravelExpenseReimbursement.UpdatedAt)).AsCustom("DATETIME(6)").NotNullable()
            .WithColumn(nameof(TravelExpenseReimbursement.SubmittedAt)).AsCustom("DATETIME(6)").Nullable();

        Create.ForeignKey("FK_TravelExpenseReimbursements_OwnerUserId_Users_Id")
            .FromTable(TravelExpenseReimbursement.TableName).ForeignColumn(nameof(TravelExpenseReimbursement.OwnerUserId))
            .ToTable(User.TableName).PrimaryColumn(nameof(User.Id));

        Create.Index("IX_TravelExpenseReimbursements_OwnerUserId")
            .OnTable(TravelExpenseReimbursement.TableName)
            .OnColumn(nameof(TravelExpenseReimbursement.OwnerUserId));

        Alter.Table(Chapter.TableName)
            .AddColumn(nameof(Chapter.KeycloakDepartment)).AsString(Chapter.KeycloakDepartmentMaxLength).Nullable();
    }

    public override void Down() {
        Delete.Column(nameof(Chapter.KeycloakDepartment)).FromTable(Chapter.TableName);
        DropTableIfExists(TravelExpenseReimbursement.TableName);
        DropTableIfExists(UserReceipt.TableName);
        DropTableIfExists(TravelExpenseRuleSet.TableName);
    }
}
