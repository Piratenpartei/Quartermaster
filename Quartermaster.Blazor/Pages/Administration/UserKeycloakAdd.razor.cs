using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Accreditation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Users;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

/// <summary>Adds members as users from their Keycloak account before their first login, so permissions can be set up early.</summary>
public partial class UserKeycloakAdd {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    private List<KeycloakUserCandidateDTO>? Candidates;
    private bool SearchBusy;
    private bool NotConfigured;
    private string? AddingId;

    private async Task SearchAsync(KeycloakAccountQuery query) {
        SearchBusy = true;
        NotConfigured = false;
        Candidates = null;
        try {
            var url = $"/api/users/keycloak-accounts?memberNumber={Uri.EscapeDataString(query.MemberNumber ?? "")}&query={Uri.EscapeDataString(query.Query ?? "")}";
            var response = await Http.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable) {
                NotConfigured = true;
                return;
            }
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            Candidates = await response.Content.ReadFromJsonAsync<List<KeycloakUserCandidateDTO>>() ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            SearchBusy = false;
        }
    }

    private async Task AddAsync(KeycloakUserCandidateDTO candidate) {
        AddingId = candidate.Account.KeycloakUserId;
        try {
            var response = await Http.PostAsJsonAsync("/api/users/keycloak-accounts", new KeycloakUserImportRequest { KeycloakUserId = candidate.Account.KeycloakUserId });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            var imported = await response.Content.ReadFromJsonAsync<KeycloakUserImportedDTO>();
            ToastService.ToastKey(I18nKey.Ui.UserKeycloakAdd.Added);
            if (imported != null)
                Navigation.NavigateTo($"/Administration/Users/{imported.UserId}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            AddingId = null;
        }
    }

    private string ChapterText(KeycloakUserCandidateDTO candidate) {
        if (candidate.ChapterName != null)
            return candidate.ChapterName;
        return string.IsNullOrWhiteSpace(candidate.Account.Department) ? "---" : I18n[I18nParams.With(I18nKey.Ui.UserKeycloakAdd.UnmappedDepartment, ("department", candidate.Account.Department))];
    }
}
