using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.AuditLog;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Accreditation.Endpoints;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

/// <summary>Applies all Keycloak accounts to their existing users the way a login would (see <see cref="KeycloakUserRefresher"/>).</summary>
public class UserKeycloakRefreshEndpoint : EndpointWithoutRequest<KeycloakUserRefreshDTO> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly KeycloakUserRefresher _refresher;
    private readonly AuditLogRepository _audit;

    public UserKeycloakRefreshEndpoint(PermissionContext perms, KeycloakSettings settings, KeycloakUserRefresher refresher, AuditLogRepository audit) {
        _perms = perms;
        _settings = settings;
        _refresher = refresher;
        _audit = audit;
    }

    public override void Configure() {
        Post("/api/users/keycloak-refresh");
    }

    public override async Task HandleAsync(CancellationToken ct) {
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

        KeycloakUserRefreshDTO result;
        try {
            result = await _refresher.RefreshAsync(DateTime.UtcNow, ct);
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Refreshing the users from Keycloak failed");
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
            return;
        }

        _audit.Log("User", _perms.UserId!.Value, "KeycloakUsersRefreshed", newValue: JsonSerializer.Serialize(result));
        await SendAsync(result, cancellation: ct);
    }
}
