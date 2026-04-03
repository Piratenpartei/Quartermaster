using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class LoginSamlCallback {
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    private string? ErrorMessage;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender)
            return;

        try {
            // Read token from URL fragment via JS (fragment is not available server-side)
            var token = await JS.InvokeAsync<string?>("samlCallback.getToken");
            if (string.IsNullOrEmpty(token)) {
                ErrorMessage = "Kein Anmeldetoken erhalten.";
                StateHasChanged();
                return;
            }

            // Store token
            await JS.InvokeVoidAsync("authStorage.setToken", token);

            // Fetch session info using the token
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/session");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await Http.SendAsync(request);

            if (!response.IsSuccessStatusCode) {
                ErrorMessage = "Sitzung konnte nicht geladen werden.";
                StateHasChanged();
                return;
            }

            var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (session == null) {
                ErrorMessage = "Ungültige Sitzungsdaten.";
                StateHasChanged();
                return;
            }

            await AuthService.CompleteSamlLoginAsync(token, session);
            Navigation.NavigateTo("/", forceLoad: false);
        } catch (Exception ex) {
            ErrorMessage = $"Fehler bei der Anmeldung: {ex.Message}";
            StateHasChanged();
        }
    }
}
