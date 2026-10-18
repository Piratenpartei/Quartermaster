using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OidcSetup {
    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    private async Task RefreshClientConfig() {
        await ConfigService.LoadAsync(forceRefresh: true);
    }
}
