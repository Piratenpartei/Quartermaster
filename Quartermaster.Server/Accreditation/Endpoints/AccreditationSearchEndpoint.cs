using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Accreditation;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

public class AccreditationSearchEndpoint : EndpointWithoutRequest<List<AccreditationAccountDTO>> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly IKeycloakAdminClient _keycloak;

    public AccreditationSearchEndpoint(PermissionContext perms, KeycloakSettings settings, IKeycloakAdminClient keycloak) {
        _perms = perms;
        _settings = settings;
        _keycloak = keycloak;
    }

    public override void Configure() {
        Get("/api/accreditation/members/{MemberNumber}");
    }

    public override async Task HandleAsync(CancellationToken ct) {
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

        var memberNumber = Route<string>("MemberNumber") ?? "";
        if (!AccreditationGuard.IsValidMemberNumber(memberNumber)) {
            AddError(AccreditationGuard.InvalidMemberNumberMessage);
            await SendErrorsAsync(400, ct);
            return;
        }

        try {
            var users = await _keycloak.FindByMemberNumberAsync(memberNumber, ct);
            await SendAsync(users.Select(KeycloakAccountMapper.ToDto).ToList(), cancellation: ct);
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Searching Keycloak accounts for member {MemberNumber} failed", memberNumber);
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
        }
    }
}
