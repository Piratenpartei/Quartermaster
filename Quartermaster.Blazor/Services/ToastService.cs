using Quartermaster.Blazor.Components;

namespace Quartermaster.Blazor.Services;

public class ToastService {
    private readonly ClientConfigService _configService;

    public ToastService(ClientConfigService configService) {
        _configService = configService;
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

    internal void RemoveToast(Toast t) => Toasts.Remove(t);
}