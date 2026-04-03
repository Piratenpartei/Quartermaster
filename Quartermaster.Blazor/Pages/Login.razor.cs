using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class Login {
    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private string? ErrorMessage;

    protected override async Task OnInitializedAsync() {
        await ConfigService.LoadAsync(forceRefresh: true);

        var uri = new Uri(Navigation.Uri);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var error = query["error"];

        if (!string.IsNullOrEmpty(error)) {
            var supportContact = ConfigService.SsoSupportContact;
            ErrorMessage = error switch {
                "saml_no_member" => string.IsNullOrEmpty(supportContact)
                    ? "Kein Mitglied mit dieser E-Mail-Adresse gefunden. Bitte wende dich an den Support."
                    : $"Kein Mitglied mit dieser E-Mail-Adresse gefunden. Bitte wende dich an {supportContact}.",
                "saml_member_exited" => "Dein Mitgliedskonto ist nicht mehr aktiv.",
                "saml_invalid" => "Die SSO-Anmeldung ist fehlgeschlagen (ungültige Antwort).",
                "saml_signature" => "Die SSO-Anmeldung ist fehlgeschlagen (ungültige Signatur).",
                "saml_no_identity" => "Die SSO-Anmeldung ist fehlgeschlagen (keine Identität erhalten).",
                "oidc_idp_error" => "Die OpenID-Anmeldung wurde vom Identitätsanbieter abgelehnt.",
                "oidc_no_code" => "Die OpenID-Anmeldung ist fehlgeschlagen (kein Autorisierungscode erhalten).",
                "oidc_not_configured" => "OpenID Connect ist nicht konfiguriert.",
                "oidc_expired" => "Die OpenID-Anmeldung ist abgelaufen. Bitte erneut versuchen.",
                "oidc_exchange_failed" => "Die OpenID-Anmeldung ist fehlgeschlagen (Token-Austausch fehlgeschlagen).",
                "oidc_no_id_token" => "Die OpenID-Anmeldung ist fehlgeschlagen (kein ID-Token erhalten).",
                "oidc_invalid_token" => "Die OpenID-Anmeldung ist fehlgeschlagen (ungültiges Token).",
                _ => "Die SSO-Anmeldung ist fehlgeschlagen."
            };
        }
    }
}
