using System.Globalization;
using Quartermaster.Api;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Accreditation.Endpoints;

/// <summary>Shared request checks for the accreditation endpoints. Returns the HTTP status to send, or null when the request may proceed.</summary>
public static class AccreditationGuard {
    public const string InvalidMemberNumberMessage = "Bitte eine gültige Mitgliedsnummer eingeben.";

    public static int? Check(PermissionContext perms, KeycloakSettings settings) {
        if (perms.UserId == null)
            return 401;
        if (!perms.HasGlobal(PermissionIdentifier.ManageAccreditation))
            return 403;
        if (!settings.IsConfigured)
            return 503;
        return null;
    }

    public static bool IsValidMemberNumber(string? value) {
        return int.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0;
    }
}
