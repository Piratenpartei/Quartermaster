using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class UserSessions {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private List<SessionDTO>? Sessions;
    private bool Loading = true;
    private bool RevokingOthers;
    private Guid? _revokingId;
    private ConfirmDialog ConfirmDialog = default!;

    protected override async Task OnInitializedAsync() {
        await Load();
    }

    private async Task Load() {
        Loading = true;
        try {
            Sessions = await Http.GetFromJsonAsync<List<SessionDTO>>("/api/users/sessions");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private async Task RevokeSession(SessionDTO session) {
        var confirmKey = session.IsCurrent
            ? I18nKey.Ui.Confirm.SessionRevokeCurrent
            : I18nKey.Ui.Confirm.DefaultMessage;
        if (!await ConfirmDialog.ShowAsync(ToastService.Translate(confirmKey)))
            return;

        _revokingId = session.TokenId;
        StateHasChanged();
        try {
            var resp = await Http.DeleteAsync($"/api/users/sessions/{session.TokenId}");
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                _revokingId = null;
                return;
            }
            if (session.IsCurrent) {
                // The next authenticated request will 401 → existing token-expired
                // handler in CsrfDelegatingHandler/MainLayout redirects to /Login.
                // Force-navigate now so the user doesn't see a stale "all good" UI.
                Navigation.NavigateTo("/Login", forceLoad: true);
                return;
            }
            ToastService.ToastKey(I18nKey.Ui.Toast.SessionRevoked);
            await Load();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            _revokingId = null;
            StateHasChanged();
        }
    }

    private async Task RevokeOthers() {
        if (!await ConfirmDialog.ShowAsync(ToastService.Translate(I18nKey.Ui.Confirm.SessionRevokeOthers)))
            return;

        RevokingOthers = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsync("/api/users/sessions/revoke-others", null);
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                return;
            }
            ToastService.ToastKey(I18nKey.Ui.Toast.SessionOthersRevoked);
            await Load();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            RevokingOthers = false;
            StateHasChanged();
        }
    }
}
