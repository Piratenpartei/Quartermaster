using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseIdRequest {
    public Guid Id { get; set; }
}

public class TravelExpenseGetEndpoint : Endpoint<TravelExpenseIdRequest, TravelExpenseResponseDTO> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public TravelExpenseGetEndpoint(TravelExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forms/travel-expense/{Id}");
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

        await SendAsync(form.ToResponse(), cancellation: ct);
    }
}
