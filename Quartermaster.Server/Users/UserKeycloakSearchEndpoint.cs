using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.Accreditation;
using Quartermaster.Api.Users;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Accreditation.Endpoints;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>Keycloak accounts to add as users before their first login, each with its existing user if there is one.</summary>
public class UserKeycloakSearchEndpoint : Endpoint<KeycloakAccountQuery, List<KeycloakUserCandidateDTO>> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly KeycloakUserImporter _importer;

    public UserKeycloakSearchEndpoint(PermissionContext perms, KeycloakSettings settings, IKeycloakAdminClient keycloak, KeycloakUserImporter importer) {
        _perms = perms;
        _settings = settings;
        _keycloak = keycloak;
        _importer = importer;
    }

    public override void Configure() {
        Get("/api/users/keycloak-accounts");
    }

    public override async Task HandleAsync(KeycloakAccountQuery req, CancellationToken ct) {
        var status = KeycloakGuard.Check(_perms, _settings, PermissionIdentifier.CreateUser);
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

        var problem = KeycloakAccountFinder.Problem(req);
        if (problem != null) {
            AddError(problem);
            await SendErrorsAsync(400, ct);
            return;
        }

        try {
            var accounts = await KeycloakAccountFinder.FindAsync(_keycloak, req, ct);
            await SendAsync(accounts.Select(_importer.Describe).ToList(), cancellation: ct);
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Searching Keycloak accounts for {MemberNumber} / {Query} failed", req.MemberNumber, req.Query);
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
        }
    }
}
