using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

public class TravelExpenseRuleScopesEndpoint : EndpointWithoutRequest<TravelExpenseRuleScopesDTO> {
    private readonly PermissionContext _perms;

    public TravelExpenseRuleScopesEndpoint(PermissionContext perms) {
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/travel-expense-rules/scopes");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var canEditGlobal = _perms.HasGlobal(PermissionIdentifier.EditTravelExpenseRules);
        // Edit permissions do not inherit to sub-chapters, while the permitted ids include them; keep the editable ones.
        var chapterIds = canEditGlobal
            ? null
            : _perms.GetPermittedChapterIds(PermissionIdentifier.EditTravelExpenseRules)!
                .Where(id => TravelExpenseRulePermissions.CanEdit(_perms, id))
                .ToList();

        await SendAsync(new TravelExpenseRuleScopesDTO { CanEditGlobal = canEditGlobal, ChapterIds = chapterIds }, cancellation: ct);
    }
}
