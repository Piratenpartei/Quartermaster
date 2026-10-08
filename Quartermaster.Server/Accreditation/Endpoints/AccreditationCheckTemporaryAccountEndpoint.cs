using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Accreditation;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

public class AccreditationCheckTemporaryAccountEndpoint : Endpoint<TemporaryAccountRequest, TemporaryAccountCheckDTO> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly TemporaryAccountChecker _checker;

    public AccreditationCheckTemporaryAccountEndpoint(PermissionContext perms, KeycloakSettings settings, TemporaryAccountChecker checker) {
        _perms = perms;
        _settings = settings;
        _checker = checker;
    }

    public override void Configure() {
        Post("/api/accreditation/temporary-accounts/check");
    }

    public override async Task HandleAsync(TemporaryAccountRequest req, CancellationToken ct) {
        var status = AccreditationGuard.Check(_perms, _settings);
        if (status == 401) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (status == 403) {
            await SendForbiddenAsync(ct);
            return;
        }
        if (status == 503) {
            AddError(KeycloakErrorMessages.NotConfigured);
            await SendErrorsAsync(503, ct);
            return;
        }

        try {
            await SendAsync(await _checker.CheckAsync(req, ct), cancellation: ct);
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Checking a temporary account for member {MemberNumber} failed", req.MemberNumber);
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
        }
    }
}
