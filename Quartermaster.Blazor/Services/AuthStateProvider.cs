using System;
using System.Threading.Tasks;
using Quartermaster.Api.Users;

namespace Quartermaster.Blazor.Services;

/// <summary>
/// Singleton holder for the Blazor WASM client's auth state. Owns the in-memory
/// <see cref="LoginResponse"/> for the current session plus the one-shot
/// <c>Initialized</c> handshake that <see cref="AuthService.InitializeAsync"/>
/// uses on app boot.
/// <para>
/// Lives separately from <see cref="AuthService"/> so the <c>CsrfDelegatingHandler</c>
/// can read auth state without depending on <c>AuthService</c> (which depends on
/// <see cref="System.Net.Http.HttpClient"/>, which goes through the handler — a cycle).
/// </para>
/// </summary>
public class AuthStateProvider {
    private readonly TaskCompletionSource _initTcs = new();
    private LoginResponse? _state;

    public bool Initialized { get; private set; }

    /// <summary>True when a <see cref="LoginResponse"/> is in memory. Cheap, no IO.</summary>
    public bool HasActiveSession => _state != null;

    /// <summary>
    /// Awaitable handshake that completes when <see cref="MarkInitialized"/> is first called.
    /// <c>CsrfDelegatingHandler</c> awaits this to avoid sending requests during the brief
    /// window between app start and the first <c>/api/users/session</c> response.
    /// </summary>
    public Task WaitForInitialization => _initTcs.Task;

    public LoginResponse? State => _state;
    public LoginUserInfo? CurrentUser => _state?.User;
    public LoginPermissions? Permissions => _state?.Permissions;

    /// <summary>Raised when an authenticated request comes back with 401 — listened to by <c>MainLayout</c>.</summary>
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
