using Quartermaster.Api;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Accreditation.Endpoints;

/// <summary>Shared request checks for the accreditation endpoints. Returns the HTTP status to send, or null when the request may proceed.</summary>
public static class AccreditationGuard {
    public const string InvalidMemberNumberMessage = "Bitte eine gültige Mitgliedsnummer eingeben.";

    public static int? Check(PermissionContext perms, KeycloakSettings settings) {
        return KeycloakGuard.Check(perms, settings, PermissionIdentifier.ManageAccreditation);
    }

    public static bool IsValidMemberNumber(string? value) {
        return MemberNumberParser.TryParse(value, out _);
    }
}
