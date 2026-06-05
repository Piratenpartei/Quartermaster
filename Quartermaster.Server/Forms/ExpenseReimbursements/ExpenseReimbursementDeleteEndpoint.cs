using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.Forms.ExpenseReimbursements;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forms.ExpenseReimbursements;

public class ExpenseReimbursementDeleteEndpoint : Endpoint<ExpenseReimbursementIdRequest> {
    private readonly ExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public ExpenseReimbursementDeleteEndpoint(ExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/forms/expense-reimbursement/{Id}");
    }

    public override async Task HandleAsync(ExpenseReimbursementIdRequest req, CancellationToken ct) {
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
            AddError("submitted forms cannot be deleted");
            await SendErrorsAsync(409, ct);
            return;
        }
        _repo.DeleteDraft(entity.Id);
        await SendOkAsync(ct);
    }
}
