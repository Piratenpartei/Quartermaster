using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Accreditation.Endpoints;

namespace Quartermaster.Server.Tests.Accreditation;

public class KeycloakErrorMessagesTests {
    [Test]
    [Arguments(401)]
    [Arguments(403)]
    public async Task Auth_failures_report_missing_permission(int statusCode) {
        var message = KeycloakErrorMessages.For(new KeycloakException(statusCode, "boom"));

        await Assert.That(message).IsEqualTo("Quartermaster hat keine Berechtigung in Keycloak.");
    }

    [Test]
    public async Task Conflict_reports_username_or_email_taken() {
        var message = KeycloakErrorMessages.For(new KeycloakException(409, "boom"));

        await Assert.That(message).IsEqualTo("Benutzername oder E-Mail ist bereits vergeben.");
    }

    [Test]
    public async Task Null_status_code_reports_unreachable() {
        var message = KeycloakErrorMessages.For(new KeycloakException(null, "boom"));

        await Assert.That(message).IsEqualTo("Keycloak ist nicht erreichbar.");
    }

    [Test]
    [Arguments("invalidPasswordGenericMessage")]
    [Arguments("invalidPasswordMinLengthMessage")]
    public async Task BadRequest_with_invalid_password_key_reports_password_policy(string errorMessageKey) {
        var message = KeycloakErrorMessages.For(new KeycloakException(400, "boom", errorMessageKey: errorMessageKey));

        await Assert.That(message).IsEqualTo("Das Passwort entspricht nicht der Passwort-Richtlinie von Keycloak. Bitte erneut versuchen.");
    }

    [Test]
    public async Task BadRequest_with_invalid_name_key_reports_invalid_name() {
        var message = KeycloakErrorMessages.For(new KeycloakException(400, "boom", errorMessageKey: "error-person-name-invalid-character"));

        await Assert.That(message).IsEqualTo("Vor- oder Nachname enthält Zeichen, die Keycloak nicht erlaubt.");
    }

    [Test]
    public async Task BadRequest_with_invalid_email_key_reports_invalid_email() {
        var message = KeycloakErrorMessages.For(new KeycloakException(400, "boom", errorMessageKey: "error-invalid-email"));

        await Assert.That(message).IsEqualTo("Die E-Mail-Adresse ist ungültig.");
    }

    [Test]
    [Arguments("error-invalid-length")]
    [Arguments("error-username-invalid-character")]
    public async Task BadRequest_with_length_or_username_key_reports_invalid_username(string errorMessageKey) {
        var message = KeycloakErrorMessages.For(new KeycloakException(400, "boom", errorMessageKey: errorMessageKey));

        await Assert.That(message).IsEqualTo("Benutzername ist ungültig.");
    }

    [Test]
    public async Task BadRequest_with_unknown_key_reports_the_generic_message() {
        var message = KeycloakErrorMessages.For(new KeycloakException(400, "boom", errorMessageKey: "something-else"));

        await Assert.That(message).IsEqualTo("Keycloak meldet einen Fehler.");
    }

    [Test]
    public async Task BadRequest_without_a_key_reports_the_generic_message() {
        var message = KeycloakErrorMessages.For(new KeycloakException(400, "boom"));

        await Assert.That(message).IsEqualTo("Keycloak meldet einen Fehler.");
    }

    [Test]
    public async Task Other_status_codes_report_the_generic_message() {
        var message = KeycloakErrorMessages.For(new KeycloakException(500, "boom"));

        await Assert.That(message).IsEqualTo("Keycloak meldet einen Fehler.");
    }
}
