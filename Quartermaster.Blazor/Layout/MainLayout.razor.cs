using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;
using Quartermaster.Blazor.Components;
using Microsoft.JSInterop;

namespace Quartermaster.Blazor.Layout;

public partial class MainLayout {
    private bool Collapsed = true;

    [Inject]
    public required AppStateService AppState { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Inject]
    public required Services.ClientConfigService ConfigService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private void ToggleMenu() {
        Collapsed = !Collapsed;
    }

    private async Task ToggleDarkMode() {
        AppState.SelectedTheme = AppState.SelectedTheme == Theme.Dark ? Theme.Light : Theme.Dark;
        await SetTheme();
    }

    protected override async Task OnInitializedAsync() {
        await ConfigService.LoadAsync();
        await AuthService.GetTokenAsync();
        await SetTheme();

        AuthService.OnTokenExpired += OnTokenExpired;
    }

    private void OnTokenExpired() {
        InvokeAsync(async () => {
            await AuthService.HandleTokenExpiredAsync();
            await AuthService.SetReturnUrlAsync(Navigation.Uri);
            StateHasChanged();
            Navigation.NavigateTo("/Login");
        });
    }

    private async Task HandleLogout() {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/", forceLoad: false);
    }

    public void Dispose() {
        AuthService.OnTokenExpired -= OnTokenExpired;
    }

    private async Task SetTheme() => await JS.InvokeVoidAsync("SetTheme", AppState.SelectedTheme.ToHtmlString());
}