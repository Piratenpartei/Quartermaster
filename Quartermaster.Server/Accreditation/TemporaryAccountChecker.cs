using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.Accreditation;
using Quartermaster.Server.Accreditation.Endpoints;

namespace Quartermaster.Server.Accreditation;

/// <summary>Validates a temporary-account request against the form rules and Keycloak; throws <see cref="KeycloakException"/> when Keycloak cannot be asked.</summary>
public class TemporaryAccountChecker {
    public const string ExistingAccountMessage = "Für diese Mitgliedsnummer existiert bereits ein Konto. Bitte oben die Stimmberechtigung prüfen.";

    private readonly IKeycloakAdminClient _keycloak;
    private readonly KeycloakSettings _settings;

    public TemporaryAccountChecker(IKeycloakAdminClient keycloak, KeycloakSettings settings) {
        _keycloak = keycloak;
        _settings = settings;
    }

    /// <summary>All problems in the entered fields that need no Keycloak lookup, in form order.</summary>
    public static List<TemporaryAccountProblemDTO> ValidateFields(TemporaryAccountRequest req) {
        var problems = new List<TemporaryAccountProblemDTO>();
        if (string.IsNullOrWhiteSpace(req.FirstName))
            problems.Add(Problem("firstName", "Bitte einen Vornamen eingeben."));
        if (string.IsNullOrWhiteSpace(req.LastName))
            problems.Add(Problem("lastName", "Bitte einen Nachnamen eingeben."));
        if (!AccreditationGuard.IsValidMemberNumber(req.MemberNumber))
            problems.Add(Problem("memberNumber", AccreditationGuard.InvalidMemberNumberMessage));
        if (!IsValidEmail(req.Email))
            problems.Add(Problem("email", "Die E-Mail-Adresse ist ungültig."));
        return problems;
    }

    public async Task<TemporaryAccountCheckDTO> CheckAsync(TemporaryAccountRequest req, CancellationToken ct) {
        var result = new TemporaryAccountCheckDTO { Problems = ValidateFields(req) };

        if (AccreditationGuard.IsValidMemberNumber(req.MemberNumber)) {
            var existing = await _keycloak.FindByMemberNumberAsync(req.MemberNumber.Trim(), ct);
            if (existing.Count > 0) {
                result.Problems.Add(Problem("memberNumber", ExistingAccountMessage));
                result.ExistingAccounts = existing.Select(KeycloakAccountMapper.ToDto).ToList();
            }
        }

        await CheckUsernameAsync(req, result, ct);
        result.Ok = result.Problems.Count == 0;
        return result;
    }

    private async Task CheckUsernameAsync(TemporaryAccountRequest req, TemporaryAccountCheckDTO result, CancellationToken ct) {
        var username = req.Username?.Trim();
        if (string.IsNullOrEmpty(username)) {
            result.Problems.Add(Problem("username", "Bitte einen Benutzernamen eingeben."));
            return;
        }
        if (!AccreditationUsernameGenerator.IsValid(username)) {
            result.Problems.Add(Problem("username", "Benutzername ist ungültig."));
            return;
        }
        if (!await _keycloak.UsernameExistsAsync(username, ct))
            return;

        result.Problems.Add(Problem("username", "Benutzername ist bereits vergeben."));
        result.SuggestedUsername = await FindFreeUsernameAsync(req, username, ct);
    }

    private async Task<string?> FindFreeUsernameAsync(TemporaryAccountRequest req, string takenUsername, CancellationToken ct) {
        var baseName = AccreditationUsernameGenerator.BuildBaseName(req.FirstName, req.LastName, _settings.UsernameSeparator);
        if (baseName.Length == 0)
            return null;
        if (!string.Equals(baseName, takenUsername, StringComparison.OrdinalIgnoreCase) && !await _keycloak.UsernameExistsAsync(baseName, ct))
            return baseName;
        if (!AccreditationGuard.IsValidMemberNumber(req.MemberNumber))
            return null;

        var suffixed = $"{baseName}{_settings.UsernameSeparator}{AccreditationUsernameGenerator.HashSuffix(req.MemberNumber)}";
        if (await _keycloak.UsernameExistsAsync(suffixed, ct))
            return null;
        return suffixed;
    }

    private static bool IsValidEmail(string? email) {
        if (string.IsNullOrWhiteSpace(email))
            return true;
        var trimmed = email.Trim();
        return trimmed.Contains('@') && !trimmed.Any(char.IsWhiteSpace);
    }

    private static TemporaryAccountProblemDTO Problem(string field, string message) => new() { Field = field, Message = message };
}
