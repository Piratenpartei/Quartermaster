using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRuleListRequest {
    /// <summary>Null lists the global default.</summary>
    [QueryParam]
    public Guid? ChapterId { get; set; }
}

public class TravelExpenseRuleListEndpoint : Endpoint<TravelExpenseRuleListRequest, List<TravelExpenseRulesDTO>> {
    private readonly TravelExpenseRuleSetRepository _repo;
    private readonly PermissionContext _perms;

    public TravelExpenseRuleListEndpoint(TravelExpenseRuleSetRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/travel-expense-rules");
    }

    public override async Task HandleAsync(TravelExpenseRuleListRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!TravelExpenseRulePermissions.CanEdit(_perms, req.ChapterId)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var versions = _repo.ListFor(req.ChapterId)
            .Select(r => r.ToDto())
            .ToList();
        await SendAsync(versions, cancellation: ct);
    }
}
