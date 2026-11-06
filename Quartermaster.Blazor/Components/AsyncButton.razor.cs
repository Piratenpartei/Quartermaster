using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

/// <summary>
/// A button for an action that waits on the server: busy while it runs, then done or failed on the button itself for
/// <see cref="AsyncButtonState.ResetDelay"/>, then ready again. The page still shows the error toast with the server's reason.
/// </summary>
public partial class AsyncButton : IDisposable {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public string Text { get; set; } = "";

    /// <summary>Bootstrap Icons name without the <c>bi-</c> prefix.</summary>
    [Parameter]
    public string Icon { get; set; } = "";

    [Parameter]
    public ButtonKind Kind { get; set; } = ButtonKind.Primary;

    /// <summary>Runs the action; returns whether it succeeded. A thrown exception counts as failed.</summary>
    [Parameter, EditorRequired]
    public Func<Task<bool>> Action { get; set; } = default!;

    /// <summary>Text after success; defaults to "Gespeichert".</summary>
    [Parameter]
    public string? DoneText { get; set; }

    [Parameter]
    public bool Small { get; set; }

    /// <summary>Applies while ready only, so the result stays readable when the action itself disables the button (a saved form has no changes left).</summary>
    [Parameter]
    public bool Disabled { get; set; }

    private readonly AsyncButtonState _state = new();

    private string CssClasses => Small ? $"btn {ButtonClass} btn-sm" : $"btn {ButtonClass}";

    private string ButtonClass => _state.Phase switch {
        AsyncButtonPhase.Done => "btn-success",
        AsyncButtonPhase.Failed => "btn-danger",
        _ => KindClass
    };

    private string KindClass => Kind switch {
        ButtonKind.Secondary => "btn-outline-secondary",
        ButtonKind.Danger => "btn-outline-danger",
        _ => "btn-primary"
    };

    private string IsHidden(params AsyncButtonPhase[] visibleIn) {
        return visibleIn.Contains(_state.Phase) ? "false" : "true";
    }

    private async Task RunAsync() {
        if (!_state.TryStart())
            return;

        var succeeded = false;
        try {
            succeeded = await Action();
        } catch (Exception ex) {
            Console.Error.WriteLine($"AsyncButton '{Text}': the action failed. {ex}");
        }
        if (!_state.Finish(succeeded))
            return;
        StateHasChanged();

        if (await _state.ResetAfterDelayAsync(AsyncButtonState.ResetDelay))
            StateHasChanged();
    }

    public void Dispose() {
        _state.Dispose();
        GC.SuppressFinalize(this);
    }
}
