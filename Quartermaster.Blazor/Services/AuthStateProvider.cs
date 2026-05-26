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
    private LoginResponse? _state;

    public bool Initialized { get; private set; }

    public bool HasActiveSession => _state != null;

    /// <summary>
    /// Completes when <see cref="MarkInitialized"/> is first called. <c>CsrfDelegatingHandler</c>
    /// awaits this to avoid racing the first <c>/api/users/session</c> on boot.
    /// </summary>
    public Task WaitForInitialization => _initTcs.Task;

    public LoginResponse? State => _state;
    public LoginUserInfo? CurrentUser => _state?.User;
    public LoginPermissions? Permissions => _state?.Permissions;

    /// <summary>Raised on 401 from an authenticated request; <c>MainLayout</c> redirects to /Login.</summary>
    public event Action? OnTokenExpired;

    public void MarkInitialized() {
        Initialized = true;
        _initTcs.TrySetResult();
    }

    public void SetState(LoginResponse? state) {
        _state = state;
    }

    public void NotifyTokenExpired() {
        if (!Initialized)
            return;
        OnTokenExpired?.Invoke();
    }
}
