using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementUpdateRequest : ExpenseReimbursementInputDTO {
    public Guid Id { get; set; }
}

public class ExpenseReimbursementUpdateEndpoint : Endpoint<ExpenseReimbursementUpdateRequest> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementUpdateEndpoint(ExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/forms/expense-reimbursement/{Id}");
    }

    public override async Task HandleAsync(ExpenseReimbursementUpdateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var entity = _repo.Get(req.Id);
        if (entity == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (entity.OwnerUserId != _perms.UserId.Value) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (entity.Status != ExpenseReimbursementStatus.Draft) {
            AddError("form is already submitted and cannot be edited");
            await SendErrorsAsync(409, ct);
            return;
        }

        var now = DateTime.UtcNow;
        entity.ApplyInputToDraft(req, now);
        _repo.UpdateDraft(entity);

        var newLineItems = req.LineItems
            .Select((it, index) => new ExpenseReimbursementLineItem {
                Id = Guid.NewGuid(),
                ExpenseReimbursementId = entity.Id,
                SortOrder = index,
                Description = it.Description,
                Value = it.Value
            })
            .ToList();
        _repo.ReplaceLineItems(entity.Id, newLineItems);

        await SendOkAsync(ct);
    }
}
