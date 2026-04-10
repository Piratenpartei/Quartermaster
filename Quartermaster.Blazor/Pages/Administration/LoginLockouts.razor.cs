using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class LoginLockouts {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private LoginLockoutListResponse? Response;
    private bool Loading = true;
    private string? UnlockingKey;

    protected override async Task OnInitializedAsync() {
        await Load();
    }

    private async Task Load() {
        Loading = true;
        StateHasChanged();

        try {
            Response = await Http.GetFromJsonAsync<LoginLockoutListResponse>("/api/users/lockouts");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }

    private async Task Unlock(LoginLockoutDTO lockout) {
        UnlockingKey = $"{lockout.IpAddress}|{lockout.UsernameOrEmail}";
        StateHasChanged();

        try {
            var response = await Http.PostAsJsonAsync("/api/users/lockouts/unlock", new LoginLockoutUnlockRequest {
                IpAddress = lockout.IpAddress,
                UsernameOrEmail = lockout.UsernameOrEmail
            });
            if (response.IsSuccessStatusCode) {
                ToastService.Toast("Sperre aufgehoben.", "success");
                await Load();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        UnlockingKey = null;
        StateHasChanged();
    }
}
