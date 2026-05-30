using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Services;

public class LanguageService {
    public const string DefaultLanguage = "de";
    public static readonly string[] AvailableLanguages = ["de", "en"];

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly I18nService _i18n;
    private readonly NavigationManager _navigation;

    public LanguageService(HttpClient http, IJSRuntime js, I18nService i18n, NavigationManager navigation) {
        _http = http;
        _js = js;
        _i18n = i18n;
        _navigation = navigation;
    }

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    public event Action? OnLanguageChanged;

    public async Task InitializeAsync() {
        var stored = await _js.InvokeAsync<string?>("languageStorage.getLanguage");
        var lang = !string.IsNullOrEmpty(stored) && IsSupported(stored)
            ? stored
            : await DetectBrowserLanguageAsync();
        await LoadAsync(lang);
    }

    public async Task SetLanguageAsync(string lang) {
        if (!IsSupported(lang) || lang == CurrentLanguage)
            return;
        await _js.InvokeVoidAsync("languageStorage.setLanguage", lang);
        _navigation.NavigateTo(_navigation.Uri, forceLoad: true);
    }

    private async Task<string> DetectBrowserLanguageAsync() {
        try {
            var detected = await _js.InvokeAsync<string>("languageStorage.detectBrowser");
            return IsSupported(detected) ? detected : DefaultLanguage;
        } catch (Exception ex) {
            Console.Error.WriteLine($"LanguageService: browser detect failed, using default. {ex}");
            return DefaultLanguage;
        }
    }

    private async Task LoadAsync(string lang) {
        try {
            var json = await _http.GetStringAsync($"i18n/{lang}.json");
            _i18n.Reload(json);
            CurrentLanguage = lang;
            OnLanguageChanged?.Invoke();
        } catch (Exception ex) {
            Console.Error.WriteLine($"LanguageService: load of {lang} failed, keeping current. {ex}");
        }
    }

    private static bool IsSupported(string lang) {
        foreach (var supported in AvailableLanguages) {
            if (supported == lang)
                return true;
        }
        return false;
    }
}
