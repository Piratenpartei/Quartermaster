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

    public void Error(string message = "Es ist ein Fehler aufgetreten.", string? details = null) {
        var contact = _configService.ErrorContact;
        var content = string.IsNullOrEmpty(contact) ? message : $"{message} {contact}";
        var detailText = _configService.ShowDetailedErrors ? details : null;
        Toasts.Add(new Toast { Content = content, Type = "danger", Details = detailText, DurationMs = null });
        Toaster?.UpdateToasts();
    }

    public void Error(Exception ex, string message = "Es ist ein Fehler aufgetreten.") {
        Error(message, ex.ToString());
    }

    /// <summary>
    /// Reads an HTTP error response, parses its <c>errors</c> array, translates
    /// each error code to German via <see cref="I18nService"/>, and shows the
    /// combined message as a persistent error toast. Falls back to the generic
    /// error message if the response has no parseable errors.
    /// </summary>
    public async Task ErrorAsync(HttpResponseMessage response, string fallbackMessage = "Es ist ein Fehler aufgetreten.") {
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