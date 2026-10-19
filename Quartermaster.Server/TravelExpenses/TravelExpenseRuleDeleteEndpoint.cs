using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRuleDeleteRequest {
    public Guid Id { get; set; }
}

public class TravelExpenseRuleDeleteEndpoint : Endpoint<TravelExpenseRuleDeleteRequest> {
    private readonly TravelExpenseRuleSetRepository _repo;
    private readonly PermissionContext _perms;

    public TravelExpenseRuleDeleteEndpoint(TravelExpenseRuleSetRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/travel-expense-rules/{Id}");
    }

    public override async Task HandleAsync(TravelExpenseRuleDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        var ruleSet = _repo.Get(req.Id);
        if (ruleSet == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (!TravelExpenseRulePermissions.CanEdit(_perms, ruleSet.ChapterId)) {
            await SendForbiddenAsync(ct);
            return;
        }
        // Every lookup ends at the global default, so it has to keep at least one version.
        if (ruleSet.ChapterId == null && _repo.ListFor(null).Count <= 1) {
            AddError(I18nKey.Error.TravelExpense.RulesLastGlobalVersion);
            await SendErrorsAsync(409, ct);
            return;
        }

        _repo.Delete(ruleSet.Id);
        await SendNoContentAsync(ct);
    }
}
