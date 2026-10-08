using System;

namespace Quartermaster.Server.Accreditation.Endpoints;

public static class KeycloakErrorMessages {
    public const string NotConfigured = "Keycloak ist nicht konfiguriert.";

    private const string PasswordPolicyMessage = "Das Passwort entspricht nicht der Passwort-Richtlinie von Keycloak. Bitte erneut versuchen.";
    private const string InvalidNameMessage = "Vor- oder Nachname enthält Zeichen, die Keycloak nicht erlaubt.";
    private const string InvalidEmailMessage = "Die E-Mail-Adresse ist ungültig.";
    private const string InvalidUsernameMessage = "Benutzername ist ungültig.";
    private const string GenericMessage = "Keycloak meldet einen Fehler.";

    public static string For(KeycloakException ex) {
        if (ex.StatusCode == 401 || ex.StatusCode == 403)
            return "Quartermaster hat keine Berechtigung in Keycloak.";
        if (ex.StatusCode == 409)
            return "Benutzername oder E-Mail ist bereits vergeben.";
        if (ex.StatusCode == null)
            return "Keycloak ist nicht erreichbar.";
        if (ex.StatusCode == 400)
            return ForBadRequest(ex.ErrorMessageKey);
        return GenericMessage;
    }

    private static string ForBadRequest(string? errorMessageKey) {
        if (errorMessageKey == null)
            return GenericMessage;
        if (errorMessageKey.StartsWith("invalidPassword", StringComparison.Ordinal))
            return PasswordPolicyMessage;
        if (errorMessageKey == "error-person-name-invalid-character")
            return InvalidNameMessage;
        if (errorMessageKey == "error-invalid-email")
            return InvalidEmailMessage;
        if (errorMessageKey == "error-invalid-length" || errorMessageKey.Contains("username", StringComparison.OrdinalIgnoreCase))
            return InvalidUsernameMessage;
        return GenericMessage;
    }
}
