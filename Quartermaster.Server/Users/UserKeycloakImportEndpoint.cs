using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Accreditation.Endpoints;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>Creates the user for a Keycloak account before its first login; the account is read again from Keycloak, never taken from the request.</summary>
public class UserKeycloakImportEndpoint : Endpoint<KeycloakUserImportRequest, KeycloakUserImportedDTO> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly KeycloakUserImporter _importer;

    public UserKeycloakImportEndpoint(PermissionContext perms, KeycloakSettings settings, IKeycloakAdminClient keycloak, KeycloakUserImporter importer) {
        _perms = perms;
        _settings = settings;
        _keycloak = keycloak;
        _importer = importer;
    }

    public override void Configure() {
        Post("/api/users/keycloak-accounts");
    }

    public override async Task HandleAsync(KeycloakUserImportRequest req, CancellationToken ct) {
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

        // Keycloak user ids are UUIDs; anything else never reaches the admin API, so no id can point the request at another path.
        if (!Guid.TryParse(req.KeycloakUserId, out var keycloakUserId)) {
            AddError(I18nKey.Error.KeycloakAccount.NotFound);
            await SendErrorsAsync(404, ct);
            return;
        }

        KeycloakUserRepresentation? account;
        try {
            account = await _keycloak.GetUserAsync(keycloakUserId.ToString(), ct);
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Reading Keycloak account {KeycloakUserId} failed", req.KeycloakUserId);
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
            return;
        }
        if (account == null) {
            AddError(I18nKey.Error.KeycloakAccount.NotFound);
            await SendErrorsAsync(404, ct);
            return;
        }

        var result = _importer.Import(account);
        if (result.Problem != null) {
            AddError(result.Problem);
            await SendErrorsAsync(result.Problem == I18nKey.Error.KeycloakAccount.UserExists ? 409 : 400, ct);
            return;
        }

        await SendAsync(new KeycloakUserImportedDTO { UserId = result.UserId!.Value }, cancellation: ct);
    }
}
