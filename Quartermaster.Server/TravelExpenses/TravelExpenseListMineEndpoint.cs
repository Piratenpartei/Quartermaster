using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseListMineEndpoint : EndpointWithoutRequest<List<TravelExpenseListItemDTO>> {
    private readonly TravelExpenseReimbursementRepository _repo;
    private readonly PermissionContext _perms;

    public TravelExpenseListMineEndpoint(TravelExpenseReimbursementRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forms/travel-expense");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var items = _repo.GetForOwner(_perms.UserId.Value)
            .Select(f => f.ToListItem())
            .ToList();
        await SendAsync(items, cancellation: ct);
    }
}
