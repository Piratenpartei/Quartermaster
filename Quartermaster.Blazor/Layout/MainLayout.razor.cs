using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Layout;

public partial class MainLayout {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required LanguageService LanguageService { get; set; }
    [Inject]
    public required ClientConfigService ConfigService { get; set; }
    [Inject]
    public required IJSRuntime JS { get; set; }

    private bool Booted;

    protected override async Task OnInitializedAsync() {
        await AuthService.InitializeAsync();
        await LanguageService.InitializeAsync();
        await ConfigService.LoadAsync();
        await JS.InvokeVoidAsync("SetTheme", AppState.SelectedTheme.ToHtmlString());
        Booted = true;
    }
}
