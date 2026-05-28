using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class SamlSetup {
    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    private static readonly string[] SamlKeys = [
        "auth.saml.endpoint",
        "auth.saml.client_id",
        "auth.saml.certificate",
        "auth.saml.button_text",
        "auth.saml.expected_audience",
        "auth.saml.expected_destination",
        "auth.sso.support_contact"
    ];

    private async Task RefreshClientConfig() {
        await ConfigService.LoadAsync(forceRefresh: true);
    }
}
