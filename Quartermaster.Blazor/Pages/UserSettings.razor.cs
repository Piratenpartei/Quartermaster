using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class UserSettings {
    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    private UserSettingsDTO? Settings;
    private bool Loading = true;
    private bool Seeding;
    private bool IsDebug => ConfigService.IsDebug;

    protected override async Task OnInitializedAsync() {
        try {
            Settings = await Http.GetFromJsonAsync<UserSettingsDTO>("/api/users/settings");
        } catch {
            Settings = null;
        }

        Loading = false;
    }

    private async Task SeedTestData() {
        Seeding = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsync("/api/testdata/seed", null);
            if (resp.IsSuccessStatusCode) {
                var result = await resp.Content.ReadAsStringAsync();
                ToastService.Toast("Testdaten erstellt.", "success");
            } else {
                var body = await resp.Content.ReadAsStringAsync();
                ToastService.Error(details: $"HTTP {(int)resp.StatusCode}: {body}");
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Seeding = false;
        StateHasChanged();
    }
}
