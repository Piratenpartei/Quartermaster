using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Accreditation;
using Quartermaster.Data.AuditLog;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

public class AccreditationSetEligibleEndpoint : EndpointWithoutRequest<AccreditationAccountDTO> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly AuditLogRepository _auditLog;

    public AccreditationSetEligibleEndpoint(PermissionContext perms, KeycloakSettings settings, IKeycloakAdminClient keycloak,
        AuditLogRepository auditLog) {
        _perms = perms;
        _settings = settings;
        _keycloak = keycloak;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Post("/api/accreditation/members/{MemberNumber}/eligible");
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
            var matches = await _keycloak.FindByMemberNumberAsync(memberNumber, ct);
            if (matches.Count == 0) {
                AddError("Kein Konto mit dieser Mitgliedsnummer.");
                await SendErrorsAsync(404, ct);
                return;
            }
            if (matches.Count > 1) {
                AddError("Mehrere Konten mit dieser Mitgliedsnummer. Bitte die IT informieren.");
                await SendErrorsAsync(409, ct);
                return;
            }

            var user = matches[0];
            var oldValue = user.Attribute("ekklesia_eligible") ?? "";
            await _keycloak.SetAttributeAsync(user.Id, "ekklesia_eligible", "true", ct);
            LogEligibilityChange(user.Id, oldValue, memberNumber);

            await SendAsync(await ReadUpdatedAccountAsync(user, memberNumber, ct), cancellation: ct);
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Setting eligibility for member {MemberNumber} failed", memberNumber);
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
        }
    }

    // The attribute is already set at this point, so a re-read failure, or the account being
    // missing from the re-read list, must still return the caller a 200: fall back to the
    // account as read before the update, with Eligible forced true.
    private async Task<AccreditationAccountDTO> ReadUpdatedAccountAsync(KeycloakUserRepresentation user, string memberNumber, CancellationToken ct) {
        try {
            var afterUpdate = await _keycloak.FindByMemberNumberAsync(memberNumber, ct);
            var updated = afterUpdate.FirstOrDefault(u => u.Id == user.Id);
            if (updated != null)
                return KeycloakAccountMapper.ToDto(updated);
            Logger.LogWarning("Re-reading member {MemberNumber} after setting eligibility no longer found account {UserId}", memberNumber, user.Id);
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            Logger.LogWarning(ex, "Re-reading member {MemberNumber} after setting eligibility failed", memberNumber);
        }

        var fallback = KeycloakAccountMapper.ToDto(user);
        fallback.Eligible = true;
        return fallback;
    }

    private void LogEligibilityChange(string userId, string oldValue, string memberNumber) {
        var oldValueWithNumber = $"{oldValue} (member {memberNumber})";
        var newValueWithNumber = $"true (member {memberNumber})";
        if (Guid.TryParse(userId, out var entityId)) {
            _auditLog.Log("KeycloakAccount", entityId, "Updated", "ekklesia_eligible", oldValueWithNumber, newValueWithNumber);
            return;
        }
        _auditLog.Log("KeycloakAccount", Guid.Empty, "Updated", $"ekklesia_eligible (keycloak id {userId})", oldValueWithNumber, newValueWithNumber);
    }
}
