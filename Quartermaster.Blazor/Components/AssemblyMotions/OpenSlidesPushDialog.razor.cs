using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class OpenSlidesPushDialog {
    [Inject]
    public required I18nService I18n { get; set; }

    private bool IsVisible;
    private bool IncludeSubmitted;
    private TaskCompletionSource<bool?>? CompletionSource;

    /// <summary>Returns whether submitted motions are included, or null when the dialog was cancelled.</summary>
    public Task<bool?> ShowAsync() {
        IncludeSubmitted = false;
        IsVisible = true;
        CompletionSource = new TaskCompletionSource<bool?>();
        StateHasChanged();
        return CompletionSource.Task;
    }

    private void Confirm() {
        IsVisible = false;
        CompletionSource?.SetResult(IncludeSubmitted);
    }

    private void Cancel() {
        IsVisible = false;
        CompletionSource?.SetResult(null);
    }
}
