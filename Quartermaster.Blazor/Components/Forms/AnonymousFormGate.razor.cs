using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Forms;

public enum GateChoice {
    Cancel,
    AcceptInMemory,
    AcceptWithStorage
}

/// <summary>
/// Modal shown to anonymous users on the form landing page, explaining the privacy trade-off and
/// offering three choices: leave (returns <see cref="GateChoice.Cancel"/>), proceed without local
/// persistence, or proceed and allow the wizard to back up draft state in <c>localStorage</c>.
/// Modeled on <see cref="ConfirmDialog"/> with a tri-state result.
/// </summary>
public partial class AnonymousFormGate {
    [Inject]
    public required I18nService I18n { get; set; }

    private bool IsVisible;
    private TaskCompletionSource<GateChoice>? _tcs;

    public Task<GateChoice> ShowAsync() {
        IsVisible = true;
        StateHasChanged();
        _tcs = new TaskCompletionSource<GateChoice>();
        return _tcs.Task;
    }

    private void Resolve(GateChoice choice) {
        IsVisible = false;
        StateHasChanged();
        _tcs?.TrySetResult(choice);
    }
}
