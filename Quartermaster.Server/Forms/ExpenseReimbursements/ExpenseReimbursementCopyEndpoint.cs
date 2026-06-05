using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementCopyEndpoint : Endpoint<ExpenseReimbursementIdRequest, ExpenseReimbursementResponseDTO> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementCopyEndpoint(ExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forms/expense-reimbursement/{Id}/copy");
    }

    public override async Task HandleAsync(ExpenseReimbursementIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var source = _repo.Get(req.Id);
        if (source == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (source.OwnerUserId != _perms.UserId.Value) {
            await SendForbiddenAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        var clone = new ExpenseReimbursement {
            Id = Guid.NewGuid(),
            OwnerUserId = source.OwnerUserId,
            Status = ExpenseReimbursementStatus.Draft,
            FirstName = source.FirstName,
            LastName = source.LastName,
            MemberNumber = source.MemberNumber,
            Street = source.Street,
            PostCode = source.PostCode,
            City = source.City,
            ChapterId = source.ChapterId,
            ChapterNameSnapshot = source.ChapterNameSnapshot,
            Function = source.Function,
            MotionDate = source.MotionDate,
            DonatedAmount = source.DonatedAmount,
            PayoutAmount = source.PayoutAmount,
            PayoutMethod = source.PayoutMethod,
            RequestsDonationReceipt = source.RequestsDonationReceipt,
            Iban = source.Iban,
            SignatureLocation = source.SignatureLocation,
            FormDate = now.Date,
            CreatedAt = now,
            UpdatedAt = now
        };

        var sourceItems = _repo.GetLineItems(source.Id);
        var cloneItems = sourceItems
            .Select((it, index) => new ExpenseReimbursementLineItem {
                Id = Guid.NewGuid(),
                ExpenseReimbursementId = clone.Id,
                SortOrder = index,
                Description = it.Description,
                Value = it.Value
            })
            .ToList();

        _repo.Create(clone, cloneItems);
        await SendAsync(clone.ToResponseDTO(cloneItems), cancellation: ct);
    }
}
