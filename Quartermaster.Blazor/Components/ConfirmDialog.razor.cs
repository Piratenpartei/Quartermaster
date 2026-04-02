using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

public partial class ConfirmDialog {
    private bool IsVisible;
    private TaskCompletionSource<bool>? _tcs;

    [Parameter]
    public string Title { get; set; } = "Bestätigung";

    [Parameter]
    public string Message { get; set; } = "Sind Sie sicher?";

    [Parameter]
    public string ConfirmText { get; set; } = "Bestätigen";

    public Task<bool> ShowAsync(string? message = null) {
        if (message != null)
            Message = message;
        IsVisible = true;
        _tcs = new TaskCompletionSource<bool>();
        StateHasChanged();
        return _tcs.Task;
    }

    private void Confirm() {
        IsVisible = false;
        _tcs?.SetResult(true);
    }

    private void Cancel() {
        IsVisible = false;
        _tcs?.SetResult(false);
    }
}
