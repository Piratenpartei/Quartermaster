using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>The rule sets of the official forms, as starting points for a chapter's own version.</summary>
public class TravelExpenseRulePresetsEndpoint : EndpointWithoutRequest<List<TravelExpenseRulePresetDTO>> {
    private readonly PermissionContext _perms;

    public TravelExpenseRulePresetsEndpoint(PermissionContext perms) {
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/travel-expense-rules/presets");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasInAnyChapter(PermissionIdentifier.EditTravelExpenseRules)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var presets = TravelExpenseRulePresets.All
            .Select(p => new TravelExpenseRulePresetDTO { Key = p.Key, Name = p.Name, Rules = p.Create() })
            .ToList();
        await SendAsync(presets, cancellation: ct);
    }
}
