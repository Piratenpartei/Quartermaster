using FluentMigrator;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.PdfTemplates;
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
    }

    public override void Down() {
        DropTableIfExists(PdfTemplate.TableName);
        DropTableIfExists(ExpenseReimbursementLineItem.TableName);
        DropTableIfExists(ExpenseReimbursement.TableName);
    }
}
