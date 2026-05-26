using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Quartermaster.Api.Users;

namespace Quartermaster.Blazor.Services;

public class AuthService {
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private LoginResponse? _loginState;

    public static bool Initialized { get; private set; }
    // Mirrors instance-level IsAuthenticated for components/handlers that can't take a DI dependency
    // on AuthService without creating a cycle (CsrfDelegatingHandler in particular).
    public static bool HasActiveSession { get; private set; }
    public static event Action? OnTokenExpired;

    private static TaskCompletionSource _initTcs = new();
    public static Task WaitForInitialization => _initTcs.Task;

    internal static void NotifyTokenExpired() {
        if (!Initialized)
            return;
        OnTokenExpired?.Invoke();
    }

    public AuthService(IJSRuntime js, HttpClient http) {
        _js = js;
        _http = http;
    }

    public bool IsAuthenticated => _loginState != null;

    private void UpdateSession(LoginResponse? state) {
        _loginState = state;
        HasActiveSession = state != null;
    }
    public LoginUserInfo? CurrentUser => _loginState?.User;
    public LoginPermissions? Permissions => _loginState?.Permissions;

    /// <summary>Reads identity from <c>/api/users/session</c>; the auth cookie (if present) rides along automatically.</summary>
    public async Task InitializeAsync() {
        // Flip Initialized BEFORE the HTTP call — CsrfDelegatingHandler waits on
        // WaitForInitialization, and our own request would deadlock otherwise.
        Initialized = true;
        _initTcs.TrySetResult();
        try {
            var response = await _http.GetAsync("/api/users/session");
            if (response.IsSuccessStatusCode)
                UpdateSession(await response.Content.ReadFromJsonAsync<LoginResponse>());
        } catch (Exception ex) {
            Console.Error.WriteLine($"AuthService.InitializeAsync: session fetch failed, treating as anonymous. {ex}");
        }
    }

    public async Task<bool> LoginAsync(string usernameOrEmail, string password) {
        var request = new LoginRequest { Password = password };
        if (usernameOrEmail.Contains('@'))
            request.Email = usernameOrEmail;
        else
            request.Username = usernameOrEmail;

        var response = await _http.PostAsJsonAsync("/api/users/login", request);
        if (!response.IsSuccessStatusCode)
            return false;

        UpdateSession(await response.Content.ReadFromJsonAsync<LoginResponse>());
        return _loginState != null;
    }

    public async Task LogoutAsync() {
        try {
            await _http.PostAsync("/api/users/logout", null);
        } catch (Exception ex) {
            Console.Error.WriteLine($"AuthService.LogoutAsync: server unreachable, clearing local state anyway. {ex}");
        }
        // Don't reset Initialized/_initTcs — the app's already past first-load and
        // anyone awaiting WaitForInitialization would hang if we re-armed the TCS.
        // Session state is tracked via HasActiveSession; that's what changes here.
        UpdateSession(null);
    }

    public Task HandleTokenExpiredAsync() {
        UpdateSession(null);
        return Task.CompletedTask;
    }

    public async Task<string?> GetReturnUrlAsync() {
        return await _js.InvokeAsync<string?>("authStorage.getReturnUrl");
    }

    public async Task SetReturnUrlAsync(string url) {
        await _js.InvokeVoidAsync("authStorage.setReturnUrl", url);
    }

    public async Task ClearReturnUrlAsync() {
        await _js.InvokeVoidAsync("authStorage.removeReturnUrl");
    }

    public bool HasGlobalPermission(string permission) {
        return _loginState?.Permissions.Global.Contains(permission) ?? false;
    }

    public bool HasChapterPermission(Guid chapterId, string permission) {
        if (_loginState?.Permissions.Chapters == null)
            return false;
        var key = chapterId.ToString();
        return _loginState.Permissions.Chapters.TryGetValue(key, out var perms) && perms.Contains(permission);
    }
}
