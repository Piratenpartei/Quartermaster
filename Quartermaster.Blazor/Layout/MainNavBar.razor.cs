using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Layout;

public partial class MainNavBar : IDisposable {
    private bool Collapsed = true;

    [Inject]
    public required AppStateService AppState { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Inject]
    public required ClientConfigService ConfigService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required AuthStateProvider AuthState { get; set; }

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
        await AuthService.InitializeAsync();
        await ConfigService.LoadAsync();
        await SetTheme();

        AuthState.OnTokenExpired += OnTokenExpired;
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
        AuthState.OnTokenExpired -= OnTokenExpired;
    }

    private async Task SetTheme() => await JS.InvokeVoidAsync("SetTheme", AppState.SelectedTheme.ToHtmlString());
}
