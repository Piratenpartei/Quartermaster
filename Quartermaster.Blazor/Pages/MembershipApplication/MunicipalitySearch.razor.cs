using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.Chapters;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class MunicipalitySearch {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private string NameQuery = "";
    private string PostCodeQuery = "";
    private List<AdministrativeDivisionDTO>? MatchingDivisions;
    private bool Searching;
    private bool HasSearched;
    private CancellationTokenSource? _searchTokenSource;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private async Task OnNameInput(ChangeEventArgs e) {
        NameQuery = e.Value?.ToString() ?? "";
        await Search();
    }

    private async Task OnPostCodeInput(ChangeEventArgs e) {
        PostCodeQuery = e.Value?.ToString() ?? "";
        await Search();
    }

    private async Task Search() {
        // Name is the primary query; fall back to the post code.
        var query = !string.IsNullOrWhiteSpace(NameQuery) ? NameQuery.Trim() : PostCodeQuery.Trim();
        if (query.Length < 2) {
            MatchingDivisions = null;
            HasSearched = false;
            StateHasChanged();
            return;
        }

        _searchTokenSource?.Cancel();
        _searchTokenSource = new CancellationTokenSource();
        var token = _searchTokenSource.Token;

        try {
            Searching = true;
            StateHasChanged();

            var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(query)}&page=1&pageSize=50", token);

            if (response != null) {
                MatchingDivisions = response.Items.FindAll(d => d.Depth == 7);
                if (MatchingDivisions.Count == 0)
                    MatchingDivisions = response.Items;
            }

            HasSearched = true;
            Searching = false;
            StateHasChanged();
        } catch (TaskCanceledException) {
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Searching = false;
            StateHasChanged();
        }
    }

    private async Task SelectDivision(AdministrativeDivisionDTO division) {
        if (EntryState == null)
            return;

        EntryState.AddressAdministrativeDivisionId = division.Id;
        EntryState.AddressCity = division.Name;
        EntryState.AddressPostCode = ResolvePostCode(division);

        await LookupChapter(division.Id);
        Navigation.NavigateTo("/MembershipApplication/AddressDetails");
    }

    private string ResolvePostCode(AdministrativeDivisionDTO division) {
        var searched = PostCodeQuery.Trim();
        if (!string.IsNullOrEmpty(searched) && division.PostCodes != null && division.PostCodes.Contains(searched))
            return searched;
        var codes = division.PostCodes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return codes != null && codes.Length > 0 ? codes[0] : searched;
    }

    private async Task LookupChapter(Guid divisionId) {
        if (EntryState == null)
            return;

        try {
            var response = await Http.GetAsync($"/api/chapters/for-division/{divisionId}");
            if (response.StatusCode == HttpStatusCode.NotFound) {
                EntryState.ChapterId = null;
                EntryState.ChapterName = null;
                return;
            }
            response.EnsureSuccessStatusCode();
            var chapter = await response.Content.ReadFromJsonAsync<ChapterDTO>();
            if (chapter != null) {
                EntryState.ChapterId = chapter.Id;
                EntryState.ChapterName = chapter.Name;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            EntryState.ChapterId = null;
            EntryState.ChapterName = null;
        }
    }

    private void EnterManually() {
        if (EntryState == null)
            return;

        // Manual entry is the last resort: drop any matched municipality so the next page
        // starts blank rather than carrying a stale selection.
        EntryState.AddressAdministrativeDivisionId = null;
        EntryState.ChapterId = null;
        EntryState.ChapterName = null;
        EntryState.AddressCity = "";
        EntryState.AddressPostCode = "";
        Navigation.NavigateTo("/MembershipApplication/AddressDetails");
    }
}
