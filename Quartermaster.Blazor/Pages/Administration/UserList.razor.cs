using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public class UserListItem {
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public partial class UserList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private List<UserListItem>? Users;
    private bool Loading = true;
    private bool Refreshing;
    private KeycloakUserRefreshDTO? RefreshResult;

    protected override async Task OnInitializedAsync() {
        try {
            Users = await Http.GetFromJsonAsync<List<UserListItem>>("/api/users");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private async Task RefreshAsync() {
        Refreshing = true;
        RefreshResult = null;
        try {
            var response = await Http.PostAsync("/api/users/keycloak-refresh", null);
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            RefreshResult = await response.Content.ReadFromJsonAsync<KeycloakUserRefreshDTO>();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Refreshing = false;
        }
    }

    private string RefreshSummary(KeycloakUserRefreshDTO result) {
        return I18n[I18nParams.With(I18nKey.Ui.UserList.RefreshResult,
            ("read", result.AccountsRead.ToString()),
            ("ids", result.KeycloakIdsStored.ToString()),
            ("chapters", result.ChaptersAssigned.ToString()),
            ("voting", result.VotingRightsChanged.ToString()),
            ("without", result.AccountsWithoutUser.ToString()))];
    }
}
