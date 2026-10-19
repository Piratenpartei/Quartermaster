using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseDeleteEndpoint : Endpoint<TravelExpenseIdRequest> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly AuditLogRepository _audit;
    private readonly PermissionContext _perms;

    public TravelExpenseDeleteEndpoint(TravelExpenseReimbursementRepository repo, AuditLogRepository audit, PermissionContext perms) {
        _repo = repo;
        _audit = audit;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/forms/travel-expense/{Id}");
    }

    public override async Task HandleAsync(TravelExpenseIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var form = _repo.Get(req.Id);
        if (form == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (form.OwnerUserId != _perms.UserId.Value) {
            await SendForbiddenAsync(ct);
            return;
        }

        _repo.Delete(form.Id);
        _audit.LogDeleted(nameof(TravelExpenseReimbursement), form.Id);
        await SendOkAsync(ct);
    }
}
