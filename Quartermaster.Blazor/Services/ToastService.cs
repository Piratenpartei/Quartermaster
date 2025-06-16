using Quartermaster.Blazor.Components;

namespace Quartermaster.Blazor.Services;

public class ToastService {
    internal Toaster? Toaster { get; set; }
    internal List<Toast> Toasts { get; } = [];

    public void Toast(string str) {
        Toasts.Add(new Toast { Content = str });
        Toaster?.UpdateToasts();
    }

    public void Toast(string str, string type) {
        Toasts.Add(new Toast { Content = str, Type = type });
        Toaster?.UpdateToasts();
    }

    internal void RemoveToast(Toast t) => Toasts.Remove(t);
}