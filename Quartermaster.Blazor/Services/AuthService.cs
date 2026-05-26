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
    private readonly AuthStateProvider _state;

    public AuthService(IJSRuntime js, HttpClient http, AuthStateProvider state) {
        _js = js;
        _http = http;
        _state = state;
    }

    public bool IsAuthenticated => _state.HasActiveSession;
    public LoginUserInfo? CurrentUser => _state.CurrentUser;
    public LoginPermissions? Permissions => _state.Permissions;

    /// <summary>Reads identity from <c>/api/users/session</c>; the auth cookie (if present) rides along automatically.</summary>
    public async Task InitializeAsync() {
        // Flip Initialized BEFORE the HTTP call — CsrfDelegatingHandler waits on
        // WaitForInitialization, and our own request would deadlock otherwise.
        _state.MarkInitialized();
        try {
            var response = await _http.GetAsync("/api/users/session");
            if (response.IsSuccessStatusCode)
                _state.SetState(await response.Content.ReadFromJsonAsync<LoginResponse>());
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

        _state.SetState(await response.Content.ReadFromJsonAsync<LoginResponse>());
        return _state.HasActiveSession;
    }

    public async Task LogoutAsync() {
        try {
            await _http.PostAsync("/api/users/logout", null);
        } catch (Exception ex) {
            Console.Error.WriteLine($"AuthService.LogoutAsync: server unreachable, clearing local state anyway. {ex}");
        }
        // Don't reset the Initialized handshake — the app's already past first-load and
        // anyone awaiting WaitForInitialization would hang if we re-armed it.
        _state.SetState(null);
    }

    public Task HandleTokenExpiredAsync() {
        _state.SetState(null);
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
        return _state.Permissions?.Global.Contains(permission) ?? false;
    }

    public bool HasChapterPermission(Guid chapterId, string permission) {
        var chapters = _state.Permissions?.Chapters;
        if (chapters == null)
            return false;
        return chapters.TryGetValue(chapterId.ToString(), out var perms) && perms.Contains(permission);
    }
}
