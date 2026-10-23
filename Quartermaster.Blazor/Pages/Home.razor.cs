using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Dashboard;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class Home : IDisposable {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required AuthStateProvider AuthState { get; set; }

    private DashboardDTO? Dashboard;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        AuthState.OnStateChanged += OnAuthStateChanged;
        await LoadAsync();
    }

    /// <summary>The sections depend on who is signed in, so logging in or out on this page loads them again.</summary>
    private void OnAuthStateChanged() {
        InvokeAsync(async () => {
            await LoadAsync();
            StateHasChanged();
        });
    }

    private async Task LoadAsync() {
        try {
            Dashboard = await Http.GetFromJsonAsync<DashboardDTO>("/api/dashboard");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    public void Dispose() {
        AuthState.OnStateChanged -= OnAuthStateChanged;
    }

    private bool HasOpenItems() {
        if (Dashboard == null)
            return false;

        return Dashboard.OpenMotions?.Items.Count > 0 || Dashboard.PendingApplications?.Items.Count > 0 || Dashboard.PendingDueSelections?.Items.Count > 0;
    }
}
