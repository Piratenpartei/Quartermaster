using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

public partial class SettingsAssistantPage {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public string Slug { get; set; } = "";

    private SettingsAssistant? Assistant;

    protected override void OnParametersSet() {
        Assistant = SettingsAssistantCatalog.BySlug(Slug);
    }
}
