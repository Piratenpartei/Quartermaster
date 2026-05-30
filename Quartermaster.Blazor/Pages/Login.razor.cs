using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class Login {
    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

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
                    ? I18n[I18nKey.Ui.Login.ErrorSamlNoMemberNoSupport]
                    : I18n[$"{I18nKey.Ui.Login.ErrorSamlNoMemberWithSupport}?support={supportContact}"],
                "saml_member_exited" => I18n[I18nKey.Ui.Login.ErrorSamlMemberExited],
                "saml_invalid" => I18n[I18nKey.Ui.Login.ErrorSamlInvalid],
                "saml_signature" => I18n[I18nKey.Ui.Login.ErrorSamlSignature],
                "saml_no_identity" => I18n[I18nKey.Ui.Login.ErrorSamlNoIdentity],
                "oidc_idp_error" => I18n[I18nKey.Ui.Login.ErrorOidcIdpError],
                "oidc_no_code" => I18n[I18nKey.Ui.Login.ErrorOidcNoCode],
                "oidc_not_configured" => I18n[I18nKey.Ui.Login.ErrorOidcNotConfigured],
                "oidc_expired" => I18n[I18nKey.Ui.Login.ErrorOidcExpired],
                "oidc_exchange_failed" => I18n[I18nKey.Ui.Login.ErrorOidcExchangeFailed],
                "oidc_no_id_token" => I18n[I18nKey.Ui.Login.ErrorOidcNoIdToken],
                "oidc_invalid_token" => I18n[I18nKey.Ui.Login.ErrorOidcInvalidToken],
                _ => I18n[I18nKey.Ui.Login.ErrorSsoGeneric]
            };
        }
    }
}
