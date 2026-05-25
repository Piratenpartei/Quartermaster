using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class ConfirmDialog {
    [Inject]
    public required I18nService I18n { get; set; }

    private bool IsVisible;
    private TaskCompletionSource<bool>? _tcs;

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? Message { get; set; }

    [Parameter]
    public string? ConfirmText { get; set; }

    private string ResolvedTitle => Title ?? I18n.Translate(I18nKey.Ui.Confirm.DefaultTitle);
    private string ResolvedMessage => Message ?? I18n.Translate(I18nKey.Ui.Confirm.DefaultMessage);
    private string ResolvedConfirmText => ConfirmText ?? I18n.Translate(I18nKey.Ui.Confirm.DefaultButton);

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
