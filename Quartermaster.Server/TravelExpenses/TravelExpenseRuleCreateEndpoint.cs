using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRuleCreateRequest : TravelExpenseRulesDTO { }

public class TravelExpenseRuleCreateValidator : Validator<TravelExpenseRuleCreateRequest> {
    public TravelExpenseRuleCreateValidator() {
        Include(new TravelExpenseRulesValidation());
    }
}

public class TravelExpenseRuleCreateEndpoint : Endpoint<TravelExpenseRuleCreateRequest, TravelExpenseRulesDTO> {
    private readonly TravelExpenseRuleSetRepository _repo;
    private readonly ChapterRepository _chapters;
    private readonly PermissionContext _perms;

    public TravelExpenseRuleCreateEndpoint(TravelExpenseRuleSetRepository repo, ChapterRepository chapters, PermissionContext perms) {
        _repo = repo;
        _chapters = chapters;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/travel-expense-rules");
    }

    public override async Task HandleAsync(TravelExpenseRuleCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!TravelExpenseRulePermissions.CanEdit(_perms, req.ChapterId)) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (req.ChapterId != null && _chapters.Get(req.ChapterId.Value) == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (_repo.HasVersionFrom(req.ChapterId, req.ValidFrom, null)) {
            AddError(I18nKey.Error.TravelExpense.RulesDuplicateValidFrom);
            await SendErrorsAsync(409, ct);
            return;
        }

        var ruleSet = new TravelExpenseRuleSet {
            Id = Guid.NewGuid(),
            ChapterId = req.ChapterId,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByUserId = _perms.UserId
        };
        ruleSet.Apply(req);
        _repo.Create(ruleSet);

        await SendAsync(ruleSet.ToDto(), cancellation: ct);
    }
}
