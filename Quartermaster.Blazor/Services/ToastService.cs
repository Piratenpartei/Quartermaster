using System.Net.Http;
using System.Threading.Tasks;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Http;

namespace Quartermaster.Blazor.Services;

public class ToastService {
    private readonly ClientConfigService _configService;
    private readonly I18nService _i18n;

    public ToastService(ClientConfigService configService, I18nService i18n) {
        _configService = configService;
        _i18n = i18n;
    }

    internal Toaster? Toaster { get; set; }
    internal List<Toast> Toasts { get; } = [];

    private const int DefaultSuccessDurationMs = 3000;

    public void Toast(string str) {
        Toasts.Add(new Toast { Content = str, DurationMs = DefaultSuccessDurationMs });
        Toaster?.UpdateToasts();
    }

    public void Toast(string str, string type) {
        var duration = type == "danger" ? (int?)null : DefaultSuccessDurationMs;
        Toasts.Add(new Toast { Content = str, Type = type, DurationMs = duration });
        Toaster?.UpdateToasts();
    }

    /// <summary>Translates <paramref name="key"/> via <see cref="I18nService"/> and shows the result as a toast.</summary>
    public void ToastKey(string key, string type = "success") {
        Toast(_i18n.Translate(key), type);
    }

    /// <summary>Translates <paramref name="key"/> via <see cref="I18nService"/> and shows the result as an error toast.</summary>
    public void ErrorKey(string key) {
        Error(_i18n.Translate(key));
    }

    public void Error(string? message = null, string? details = null) {
        var resolved = message ?? _i18n.Translate(I18nKey.Ui.Error.Generic);
        var contact = _configService.ErrorContact;
        var content = string.IsNullOrEmpty(contact) ? resolved : $"{resolved} {contact}";
        var detailText = _configService.ShowDetailedErrors ? details : null;
        Toasts.Add(new Toast { Content = content, Type = "danger", Details = detailText, DurationMs = null });
        Toaster?.UpdateToasts();
    }

    public void Error(Exception ex, string? message = null) {
        Error(message, ex.ToString());
    }

    /// <summary>
    /// Reads an HTTP error response, parses its <c>errors</c> array, translates
    /// each error code via <see cref="I18nService"/>, and shows the combined
    /// message as a persistent error toast. Falls back to the generic UI error
    /// when the response has no parseable error payload.
    /// </summary>
    public async Task ErrorAsync(HttpResponseMessage response, string? fallbackMessage = null) {
        var combined = await ApiErrorHelper.GetCombinedErrorMessageAsync(response, _i18n);
        if (combined != null) {
            Error(combined);
        } else {
            Error(fallbackMessage, details: $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    /// <summary>
    /// Translates a single i18n key (optionally with parameters) and returns
    /// the localized string. Useful for callers that need the translated text
    /// without showing a toast.
    /// </summary>
    public string Translate(string key) => _i18n.Translate(key);

    internal void RemoveToast(Toast t) => Toasts.Remove(t);
}