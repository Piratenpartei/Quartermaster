using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Accreditation;
using Quartermaster.Data.AuditLog;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

public class AccreditationCreateTemporaryAccountEndpoint : Endpoint<TemporaryAccountRequest> {
    private readonly PermissionContext _perms;
    private readonly KeycloakSettings _settings;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly AuditLogRepository _auditLog;

    public AccreditationCreateTemporaryAccountEndpoint(PermissionContext perms, KeycloakSettings settings, IKeycloakAdminClient keycloak,
        AuditLogRepository auditLog) {
        _perms = perms;
        _settings = settings;
        _keycloak = keycloak;
        _auditLog = auditLog;
    }

    public override void Configure() {
        Post("/api/accreditation/temporary-accounts");
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

        var fieldProblems = TemporaryAccountChecker.ValidateFields(req);
        if (fieldProblems.Count > 0) {
            AddError(fieldProblems[0].Message);
            await SendErrorsAsync(400, ct);
            return;
        }

        var memberNumber = req.MemberNumber.Trim();
        string userId;
        string username;
        string password;
        bool eligible;

        try {
            var existing = await _keycloak.FindByMemberNumberAsync(memberNumber, ct);
            if (existing.Count > 0) {
                await SendAsync(new TemporaryAccountConflictDTO {
                    Message = TemporaryAccountChecker.ExistingAccountMessage,
                    ExistingAccounts = existing.Select(KeycloakAccountMapper.ToDto).ToList()
                }, 409, ct);
                return;
            }

            var (resolvedUsername, usernameError) = await ResolveUsernameAsync(req, ct);
            if (usernameError != null) {
                AddError(usernameError);
                await SendErrorsAsync(400, ct);
                return;
            }
            username = resolvedUsername!;

            password = ReadablePasswordGenerator.Generate();
            eligible = req.Eligible;
            var attributes = new Dictionary<string, List<string>> {
                ["member_number"] = [memberNumber],
                ["ekklesia_verified"] = ["true"],
                ["ekklesia_eligible"] = [eligible ? "true" : "false"],
                ["ekklesia_external_voting"] = ["false"],
                ["created_time"] = [DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'+00:00'", CultureInfo.InvariantCulture)]
            };
            var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
            var newUser = new NewKeycloakUser(username, req.FirstName.Trim(), req.LastName.Trim(), email, password, attributes);

            var createdUserId = await CreateAccountAsync(newUser, memberNumber, ct);
            if (createdUserId == null)
                return;
            userId = createdUserId;
        } catch (KeycloakException ex) {
            Logger.LogWarning(ex, "Creating a temporary account for member {MemberNumber} failed", memberNumber);
            AddError(KeycloakErrorMessages.For(ex));
            await SendErrorsAsync(502, ct);
            return;
        }

        // The account now exists in Keycloak. Everything below is bookkeeping (audit entry,
        // duplicate re-check) that must never turn a successful create into an error response
        // and strand the caller without the password - failures become a Warning instead.
        var warning = await FinalizeAccountCreationAsync(userId, memberNumber, username, eligible, ct);

        await SendAsync(new TemporaryAccountCreatedDTO {
            KeycloakUserId = userId,
            Username = username.ToLowerInvariant(),
            Password = password,
            Warning = warning
        }, cancellation: ct);
    }

    // CreateUserAsync can fail with StatusCode null when Keycloak was unreachable or timed out; the
    // request may or may not have gone through server-side. A re-check by member number tells the
    // delegate whether an orphaned account (with an unknown password) may now exist.
    private async Task<string?> CreateAccountAsync(NewKeycloakUser newUser, string memberNumber, CancellationToken ct) {
        try {
            return await _keycloak.CreateUserAsync(newUser, ct);
        } catch (KeycloakException ex) when (ex.StatusCode == null) {
            if (!await AccountExistsAfterFailureAsync(memberNumber, ct))
                throw;

            Logger.LogWarning(ex, "Creating a temporary account for member {MemberNumber} may have succeeded despite a failed response", memberNumber);
            AddError("Zugang wurde möglicherweise angelegt, das Passwort ist aber unbekannt. Bitte die IT informieren.");
            await SendErrorsAsync(502, ct);
            return null;
        }
    }

    private async Task<bool> AccountExistsAfterFailureAsync(string memberNumber, CancellationToken ct) {
        try {
            var accounts = await _keycloak.FindByMemberNumberAsync(memberNumber, ct);
            return accounts.Count > 0;
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            Logger.LogWarning(ex, "Re-checking for member {MemberNumber} after a failed create failed", memberNumber);
            return false;
        }
    }

    private async Task<string?> FinalizeAccountCreationAsync(string userId, string memberNumber, string username, bool eligible,
        CancellationToken ct) {
        var warnings = new List<string>();

        try {
            LogCreated(userId, memberNumber, username, eligible);
        } catch (Exception ex) {
            Logger.LogError(ex, "Writing the audit entry for the new Keycloak account {UserId} (member {MemberNumber}) failed",
                userId, memberNumber);
            warnings.Add("Zugang angelegt, aber der Audit-Eintrag konnte nicht geschrieben werden. Bitte die IT informieren.");
        }

        try {
            var afterCreate = await _keycloak.FindByMemberNumberAsync(memberNumber, ct);
            if (afterCreate.Count > 1) {
                warnings.Add("Für diese Mitgliedsnummer gibt es jetzt mehrere Konten. Diese werden nachts nicht automatisch " +
                    "geschlossen; bitte die IT informieren, damit ein Konto gelöscht wird.");
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            Logger.LogWarning(ex, "Re-checking for duplicate accounts after creating member {MemberNumber} failed", memberNumber);
            warnings.Add("Zugang angelegt, aber die Prüfung auf doppelte Konten ist fehlgeschlagen. Bitte die IT informieren.");
        }

        return warnings.Count == 0 ? null : string.Join(" ", warnings);
    }

    private void LogCreated(string userId, string memberNumber, string username, bool eligible) {
        var entityId = Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;
        _auditLog.Log("KeycloakAccount", entityId, "Created", "temporary_account", null,
            $"member {memberNumber}, username {username}, eligible {eligible}");
    }

    private async Task<(string? Username, string? Error)> ResolveUsernameAsync(TemporaryAccountRequest req, CancellationToken ct) {
        var manual = req.Username?.Trim();
        if (!string.IsNullOrEmpty(manual))
            return await ResolveManualUsernameAsync(manual, ct);

        return await ResolveSuggestedUsernameAsync(req, ct);
    }

    private async Task<(string? Username, string? Error)> ResolveManualUsernameAsync(string manual, CancellationToken ct) {
        if (!AccreditationUsernameGenerator.IsValid(manual))
            return (null, "Benutzername ist ungültig.");
        if (await _keycloak.UsernameExistsAsync(manual, ct))
            return (null, "Benutzername ist bereits vergeben.");
        return (manual, null);
    }

    private async Task<(string? Username, string? Error)> ResolveSuggestedUsernameAsync(TemporaryAccountRequest req, CancellationToken ct) {
        var baseName = AccreditationUsernameGenerator.BuildBaseName(req.FirstName, req.LastName, _settings.UsernameSeparator);
        if (baseName.Length == 0)
            return (null, "Bitte einen Benutzernamen eingeben.");
        if (!await _keycloak.UsernameExistsAsync(baseName, ct))
            return (baseName, null);

        var suffixed = $"{baseName}{_settings.UsernameSeparator}{AccreditationUsernameGenerator.HashSuffix(req.MemberNumber)}";
        if (!await _keycloak.UsernameExistsAsync(suffixed, ct))
            return (suffixed, null);

        return (null, "Bitte einen Benutzernamen eingeben.");
    }
}
