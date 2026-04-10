using Microsoft.AspNetCore.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components;

public partial class Toaster : IDisposable {
    [Inject]
    public required ToastService ToastService { get; init; }

    private class TimerState {
        public Timer Timer { get; set; } = null!;
        public DateTime StartedAt { get; set; }
        public int RemainingMs { get; set; }
    }

    private readonly Dictionary<Toast, TimerState> _timers = [];

    protected override void OnInitialized() {
        ToastService.Toaster = this;
    }

    internal void UpdateToasts() {
        foreach (var toast in ToastService.Toasts) {
            if (toast.DurationMs.HasValue && !_timers.ContainsKey(toast))
                ScheduleAutoDismiss(toast, toast.DurationMs.Value);
        }
        StateHasChanged();
    }

    private void ScheduleAutoDismiss(Toast toast, int remainingMs) {
        var state = new TimerState {
            StartedAt = DateTime.UtcNow,
            RemainingMs = remainingMs
        };
        state.Timer = new Timer(_ => InvokeAsync(() => {
            if (_timers.Remove(toast)) {
                ToastService.RemoveToast(toast);
                StateHasChanged();
            }
        }), null, remainingMs, Timeout.Infinite);
        _timers[toast] = state;
    }

    private void OnMouseEnter(Toast toast) {
        if (!_timers.TryGetValue(toast, out var state))
            return;
        var elapsed = (int)(DateTime.UtcNow - state.StartedAt).TotalMilliseconds;
        var remaining = Math.Max(0, state.RemainingMs - elapsed);
        state.Timer.Dispose();
        _timers.Remove(toast);
        // Stash remaining time so we can resume on mouse leave.
        _pausedRemaining[toast] = remaining;
    }

    private void OnMouseLeave(Toast toast) {
        if (!_pausedRemaining.Remove(toast, out var remaining))
            return;
        ScheduleAutoDismiss(toast, remaining);
    }

    private readonly Dictionary<Toast, int> _pausedRemaining = [];

    private void OnClose(Toast t) {
        if (_timers.Remove(t, out var state))
            state.Timer.Dispose();
        _pausedRemaining.Remove(t);
        ToastService.RemoveToast(t);
    }

    public void Dispose() {
        foreach (var state in _timers.Values)
            state.Timer.Dispose();
        _timers.Clear();
        _pausedRemaining.Clear();
    }
}