using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OidcSetup {
    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    private static readonly string[] OidcKeys = [
        "auth.oidc.authority",
        "auth.oidc.client_id",
        "auth.oidc.client_secret",
        "auth.oidc.button_text",
        "auth.sso.support_contact"
    ];

    private async Task RefreshClientConfig() {
        await ConfigService.LoadAsync(forceRefresh: true);
    }
}
