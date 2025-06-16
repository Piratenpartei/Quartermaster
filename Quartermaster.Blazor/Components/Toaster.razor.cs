using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

public partial class Toaster {
    [Inject]
    public required ToastService ToastService { get; init; }

    protected override void OnInitialized() {
        ToastService.Toaster = this;
    }

    internal void UpdateToasts() => StateHasChanged();

    private void OnClose(Toast t) => ToastService.RemoveToast(t);
}