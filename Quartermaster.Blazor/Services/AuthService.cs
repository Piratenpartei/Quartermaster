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

    public static string? StaticToken { get; internal set; }
    public static event Action? OnTokenExpired;

    internal static void NotifyTokenExpired() {
        StaticToken = null;
        OnTokenExpired?.Invoke();
    }

    public AuthService(IJSRuntime js, HttpClient http) {
        _js = js;
        _http = http;
    }

    public bool IsAuthenticated => _loginState != null;
    public LoginUserInfo? CurrentUser => _loginState?.User;
    public LoginPermissions? Permissions => _loginState?.Permissions;
    public string? Token { get; private set; }

    public async Task<string?> GetTokenAsync() {
        if (Token != null)
            return Token;
        Token = await _js.InvokeAsync<string?>("authStorage.getToken");
        StaticToken = Token;
        return Token;
    }

    public async Task<bool> LoginAsync(string usernameOrEmail, string password) {
        var request = new LoginRequest { Password = password };
        if (usernameOrEmail.Contains('@'))
            request.EMail = usernameOrEmail;
        else
            request.Username = usernameOrEmail;

        var response = await _http.PostAsJsonAsync("/api/users/login", request);
        if (!response.IsSuccessStatusCode)
            return false;

        _loginState = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (_loginState == null)
            return false;

        Token = _loginState.Token;
        StaticToken = Token;
        await _js.InvokeVoidAsync("authStorage.setToken", Token);
        return true;
    }

    public Task CompleteSamlLoginAsync(string token, LoginResponse session) {
        Token = token;
        StaticToken = token;
        session.Token = token;
        _loginState = session;
        return Task.CompletedTask;
    }

    public async Task LogoutAsync() {
        _loginState = null;
        Token = null;
        StaticToken = null;
        await _js.InvokeVoidAsync("authStorage.removeToken");
    }

    public async Task HandleTokenExpiredAsync() {
        _loginState = null;
        Token = null;
        StaticToken = null;
        await _js.InvokeVoidAsync("authStorage.removeToken");
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
