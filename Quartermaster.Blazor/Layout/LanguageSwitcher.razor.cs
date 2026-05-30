using System;
using System.Threading.Tasks;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Layout;

public partial class LanguageSwitcher : IDisposable {
    private bool Open;

    protected override void OnInitialized() {
        Language.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    private void Toggle() => Open = !Open;

    private void Close() => Open = false;

    private async Task SwitchTo(string lang) {
        Open = false;
        await Language.SetLanguageAsync(lang);
    }

    private static string Label(string lang) => lang switch {
        "de" => "Deutsch",
        "en" => "English",
        _ => lang
    };

    public void Dispose() {
        Language.OnLanguageChanged -= OnLanguageChanged;
    }
}
