using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Settings;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Settings;

public class SettingsCheckEndpoint : Endpoint<SettingsCheckRequest, SettingsCheckResultDTO> {
    private readonly PermissionContext _perms;
    private readonly IEnumerable<ISettingsCheck> _checks;

    public SettingsCheckEndpoint(PermissionContext perms, IEnumerable<ISettingsCheck> checks) {
        _perms = perms;
        _checks = checks;
    }

    public override void Configure() {
        Post("/api/settings/check");
    }

    public override async Task HandleAsync(SettingsCheckRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var check = _checks.FirstOrDefault(c => c.Kind == req.Kind);
        if (check == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendAsync(await check.RunAsync(ct), cancellation: ct);
    }
}
