using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

// Legacy route: the SSO endpoints now set the auth cookie server-side and redirect straight to /.
// This page survives only to handle stale bookmarks / cached redirects — re-initialize the session
// from the (now-present) cookie and forward home.
public partial class LoginSamlCallback {
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    private string? ErrorMessage;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender)
            return;
        await AuthService.InitializeAsync();
        Navigation.NavigateTo("/", forceLoad: false);
    }
}
