using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Config;
using Quartermaster.Data.Options;
using Quartermaster.Server.PublicForms;

namespace Quartermaster.Server.Config;

public class ClientConfigEndpoint : EndpointWithoutRequest<ClientConfigDTO> {
    private readonly OptionRepository _optionRepo;
    private readonly PublicFormSettings _publicForms;

    public ClientConfigEndpoint(OptionRepository optionRepo, PublicFormSettings publicForms) {
        _optionRepo = optionRepo;
        _publicForms = publicForms;
    }

    public override void Configure() {
        Get("/api/config/client");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var errorContact = _optionRepo.GetGlobalValue("general.error.contact")?.Value ?? "";
        var showDetails = _optionRepo.GetGlobalValue("general.error.show_details")?.Value ?? "false";
        var samlEndpoint = _optionRepo.GetGlobalValue("auth.saml.endpoint")?.Value ?? "";
        var samlButtonText = _optionRepo.GetGlobalValue("auth.saml.button_text")?.Value ?? "SSO Login";
        var ssoSupportContact = _optionRepo.GetGlobalValue("auth.sso.support_contact")?.Value ?? "";
        var oidcAuthority = _optionRepo.GetGlobalValue("auth.oidc.authority")?.Value ?? "";
        var oidcButtonText = _optionRepo.GetGlobalValue("auth.oidc.button_text")?.Value ?? "OpenID Login";

        await SendAsync(new ClientConfigDTO {
            ErrorContact = errorContact,
            ShowDetailedErrors = showDetails.Equals("true", System.StringComparison.OrdinalIgnoreCase),
            SamlEnabled = !string.IsNullOrEmpty(samlEndpoint),
            SamlButtonText = samlButtonText,
            SsoSupportContact = ssoSupportContact,
            OidcEnabled = !string.IsNullOrEmpty(oidcAuthority),
            OidcButtonText = oidcButtonText,
            MembershipApplicationEnabled = _publicForms.IsMembershipApplicationEnabled(),
            DueSelectionEnabled = _publicForms.IsDueSelectionEnabled(),
            MotionFormChapterIds = _publicForms.MotionFormChapterIds(),
#if DEBUG
            IsDebug = true
#else
            IsDebug = false
#endif
        }, cancellation: ct);
    }
}
