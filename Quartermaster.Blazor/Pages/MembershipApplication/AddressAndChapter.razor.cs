using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.Chapters;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class AddressAndChapter {
    [Inject]
    public required AppStateService AppState { get; set; }
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private List<AdministrativeDivisionDTO>? MatchingDivisions;
    private bool SearchingPostCode;
    private CancellationTokenSource? _searchTokenSource;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
    }

    private async Task OnPostCodeInput(ChangeEventArgs e) {
        if (EntryState == null || !EntryState.IsGermany)
            return;

        var value = e.Value?.ToString() ?? "";
        EntryState.AddressPostCode = value;

        // Reset selection when post code changes
        EntryState.AddressAdministrativeDivisionId = null;
        EntryState.AddressCity = "";
        EntryState.AddressStreet = "";
        EntryState.AddressHouseNbr = "";
        EntryState.ChapterId = null;
        EntryState.ChapterName = null;
        MatchingDivisions = null;

        // Auto-search when 5 digits entered
        if (value.Length < 5)
            return;

        _searchTokenSource?.Cancel();
        _searchTokenSource = new CancellationTokenSource();
        var token = _searchTokenSource.Token;

        try {
            SearchingPostCode = true;
            StateHasChanged();

            var response = await Http.GetFromJsonAsync<AdministrativeDivisionSearchResponse>(
                $"/api/administrativedivisions/search?query={Uri.EscapeDataString(value)}&page=1&pageSize=50",
                token);

            if (response != null) {
                MatchingDivisions = response.Items.FindAll(d => d.Depth == 7);
                if (MatchingDivisions.Count == 0)
                    MatchingDivisions = response.Items;
            }

            SearchingPostCode = false;
            StateHasChanged();
        } catch (TaskCanceledException) {
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            SearchingPostCode = false;
            StateHasChanged();
        }
    }

    private async Task SelectDivision(AdministrativeDivisionDTO division) {
        if (EntryState == null)
            return;

        EntryState.AddressAdministrativeDivisionId = division.Id;
        EntryState.AddressCity = division.Name;

        await LookupChapter(division.Id);
        StateHasChanged();
    }

    private async Task LookupChapter(Guid divisionId) {
        if (EntryState == null)
            return;

        try {
            var chapter = await Http.GetFromJsonAsync<ChapterDTO>(
                $"/api/chapters/for-division/{divisionId}");
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

    private bool CanContinue() {
        if (EntryState == null)
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressPostCode))
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressStreet))
            return false;
        if (string.IsNullOrEmpty(EntryState.AddressHouseNbr))
            return false;

        if (EntryState.IsGermany) {
            if (EntryState.AddressAdministrativeDivisionId == null)
                return false;
        } else {
            if (string.IsNullOrEmpty(EntryState.AddressCountry))
                return false;
            if (string.IsNullOrEmpty(EntryState.AddressCity))
                return false;
        }

        return true;
    }
}
