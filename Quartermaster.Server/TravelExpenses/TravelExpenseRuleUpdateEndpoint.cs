using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRuleUpdateRequest : TravelExpenseRulesDTO { }

public class TravelExpenseRuleUpdateValidator : Validator<TravelExpenseRuleUpdateRequest> {
    public TravelExpenseRuleUpdateValidator() {
        Include(new TravelExpenseRulesValidation());
    }
}

/// <summary>Changing a version also changes forms already calculated with it; the page warns about that.</summary>
public class TravelExpenseRuleUpdateEndpoint : Endpoint<TravelExpenseRuleUpdateRequest, TravelExpenseRulesDTO> {
    private readonly TravelExpenseRuleSetRepository _repo;
    private readonly PermissionContext _perms;

    public TravelExpenseRuleUpdateEndpoint(TravelExpenseRuleSetRepository repo, PermissionContext perms) {
        _repo = repo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/travel-expense-rules/{Id}");
    }

    public override async Task HandleAsync(TravelExpenseRuleUpdateRequest req, CancellationToken ct) {
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
        if (req.ChapterId != ruleSet.ChapterId) {
            AddError(I18nKey.Error.TravelExpense.RulesChapterChanged);
            await SendErrorsAsync(400, ct);
            return;
        }
        if (_repo.HasVersionFrom(ruleSet.ChapterId, req.ValidFrom, ruleSet.Id)) {
            AddError(I18nKey.Error.TravelExpense.RulesDuplicateValidFrom);
            await SendErrorsAsync(409, ct);
            return;
        }

        ruleSet.Apply(req);
        ruleSet.UpdatedAt = DateTime.UtcNow;
        ruleSet.UpdatedByUserId = _perms.UserId;
        _repo.Update(ruleSet);

        await SendAsync(ruleSet.ToDto(), cancellation: ct);
    }
}
