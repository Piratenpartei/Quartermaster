using System;
using System.Linq;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.ExpenseReimbursements;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

internal static class ExpenseReimbursementMapping {
    public static ExpenseReimbursementInputDTO ToInputDTO(this ExpenseReimbursement entity, System.Collections.Generic.IEnumerable<ExpenseReimbursementLineItem> items) {
        return new ExpenseReimbursementInputDTO {
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            MemberNumber = entity.MemberNumber,
            Street = entity.Street,
            PostCode = entity.PostCode,
            City = entity.City,
            ChapterId = entity.ChapterId,
            ChapterName = entity.ChapterNameSnapshot,
            Function = entity.Function,
            MotionDate = entity.MotionDate,
            DonatedAmount = entity.DonatedAmount,
            PayoutAmount = entity.PayoutAmount,
            PayoutMethod = (ExpensePayoutMethodDTO)(int)entity.PayoutMethod,
            RequestsDonationReceipt = entity.RequestsDonationReceipt,
            Iban = entity.Iban,
            SignatureLocation = entity.SignatureLocation,
            FormDate = entity.FormDate,
            LineItems = items
                .OrderBy(i => i.SortOrder)
                .Select(i => new ExpenseReimbursementLineItemDTO {
                    Description = i.Description,
                    Value = i.Value
                })
                .ToList()
        };
    }

    public static ExpenseReimbursementResponseDTO ToResponseDTO(this ExpenseReimbursement entity, System.Collections.Generic.IEnumerable<ExpenseReimbursementLineItem> items) {
        return new ExpenseReimbursementResponseDTO {
            Id = entity.Id,
            Status = (ExpenseReimbursementStatusDTO)(int)entity.Status,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)),
            SubmittedAt = entity.SubmittedAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.SubmittedAt.Value, DateTimeKind.Utc))
                : null,
            Input = entity.ToInputDTO(items)
        };
    }

    public static void ApplyInputToDraft(this ExpenseReimbursement entity, ExpenseReimbursementInputDTO input, DateTime now) {
        entity.FirstName = input.FirstName;
        entity.LastName = input.LastName;
        entity.MemberNumber = input.MemberNumber;
        entity.Street = input.Street;
        entity.PostCode = input.PostCode;
        entity.City = input.City;
        entity.ChapterId = input.ChapterId;
        entity.ChapterNameSnapshot = input.ChapterName;
        entity.Function = input.Function;
        entity.MotionDate = input.MotionDate;
        entity.DonatedAmount = input.DonatedAmount;
        entity.PayoutAmount = input.PayoutAmount;
        entity.PayoutMethod = (ExpensePayoutMethod)(int)input.PayoutMethod;
        entity.RequestsDonationReceipt = input.RequestsDonationReceipt;
        entity.Iban = input.Iban;
        entity.SignatureLocation = input.SignatureLocation;
        entity.FormDate = input.FormDate;
        entity.UpdatedAt = now;
    }
}
