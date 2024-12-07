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

    private void ToggleMenu() {
        Collapsed = !Collapsed;
    }

    private async Task ToggleDarkMode() {
        AppState.SelectedTheme = AppState.SelectedTheme == Theme.Dark ? Theme.Light : Theme.Dark;
        await SetTheme();
    }

    protected override async Task OnInitializedAsync() => await SetTheme();

    private async Task SetTheme() => await JS.InvokeVoidAsync("SetTheme", AppState.SelectedTheme.ToHtmlString());
}