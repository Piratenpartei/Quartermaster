using System;
using System.Threading.Tasks;
using Quartermaster.Api.Users;

namespace Quartermaster.Blazor.Services;

/// <summary>
/// Singleton holder for the Blazor WASM client's auth state. Separate from
/// <see cref="AuthService"/> so <c>CsrfDelegatingHandler</c> can read auth state
/// without the DI cycle (handler → HttpClient → AuthService → handler).
/// </summary>
public class AuthStateProvider {
    private readonly TaskCompletionSource _initTcs = new();
    private readonly TaskCompletionSource _sessionTcs = new();
    private LoginResponse? _state;

    public bool Initialized { get; private set; }

    public bool HasActiveSession => _state != null;

    /// <summary>
    /// Completes when <see cref="MarkInitialized"/> is first called. <c>CsrfDelegatingHandler</c>
    /// awaits this to avoid racing the first <c>/api/users/session</c> on boot. Fires BEFORE the
    /// session response arrives — do NOT use to gate reads of <see cref="CurrentUser"/>.
    /// </summary>
    public Task WaitForInitialization => _initTcs.Task;

    /// <summary>
    /// Completes once the initial <c>/api/users/session</c> response has been processed (success
    /// or failure). Page initializers that read <see cref="CurrentUser"/> on first render should
    /// await this to avoid the cold-load race.
    /// </summary>
    public Task WaitForSessionFetch => _sessionTcs.Task;

    public LoginResponse? State => _state;
    public LoginUserInfo? CurrentUser => _state?.User;
    public LoginPermissions? Permissions => _state?.Permissions;

    /// <summary>Raised on 401 from an authenticated request; <c>MainLayout</c> redirects to /Login.</summary>
    public event Action? OnTokenExpired;

    /// <summary>Raised whenever the auth state changes (login, logout, session refresh). Layout + permission-gated components subscribe to re-render.</summary>
    public event Action? OnStateChanged;

    public void MarkInitialized() {
        Initialized = true;
        _initTcs.TrySetResult();
    }

    public void MarkSessionFetched() {
        _sessionTcs.TrySetResult();
    }

    public void SetState(LoginResponse? state) {
        _state = state;
        OnStateChanged?.Invoke();
    }

    public void NotifyTokenExpired() {
        if (!Initialized)
            return;
        OnTokenExpired?.Invoke();
    }
}
