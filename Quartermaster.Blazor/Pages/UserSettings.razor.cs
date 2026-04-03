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

    private UserSettingsDTO? Settings;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            Settings = await Http.GetFromJsonAsync<UserSettingsDTO>("/api/users/settings");
        } catch {
            Settings = null;
        }

        Loading = false;
    }
}
