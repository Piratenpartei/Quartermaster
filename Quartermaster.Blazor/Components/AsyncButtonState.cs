using System;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Blazor.Components;

public enum AsyncButtonPhase {
    Ready,
    Busy,
    Done,
    Failed
}

/// <summary>The steps of an <see cref="AsyncButton"/>: ready, busy while the action runs, done or failed, then ready again.</summary>
public class AsyncButtonState : IDisposable {
    public static readonly TimeSpan ResetDelay = TimeSpan.FromSeconds(2);

    private readonly CancellationTokenSource _resetCancellation = new();
    private bool _disposed;

    public AsyncButtonPhase Phase { get; private set; } = AsyncButtonPhase.Ready;

    /// <summary>False while an action runs or shows its result, so a second click cannot send twice.</summary>
    public bool TryStart() {
        if (_disposed || Phase != AsyncButtonPhase.Ready)
            return false;
        Phase = AsyncButtonPhase.Busy;
        return true;
    }

    /// <summary>False when disposed or not busy, so the caller skips its re-render.</summary>
    public bool Finish(bool succeeded) {
        if (_disposed || Phase != AsyncButtonPhase.Busy)
            return false;
        Phase = succeeded ? AsyncButtonPhase.Done : AsyncButtonPhase.Failed;
        return true;
    }

    /// <summary>Waits, then returns to ready; false when disposed before or during the wait, so the caller skips its re-render.</summary>
    public async Task<bool> ResetAfterDelayAsync(TimeSpan delay) {
        if (_disposed)
            return false;
        try {
            await Task.Delay(delay, _resetCancellation.Token);
        } catch (TaskCanceledException) {
            return false;
        }
        if (_disposed)
            return false;
        Reset();
        return true;
    }

    public void Reset() {
        if (_disposed)
            return;
        Phase = AsyncButtonPhase.Ready;
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        _resetCancellation.Cancel();
        _resetCancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
