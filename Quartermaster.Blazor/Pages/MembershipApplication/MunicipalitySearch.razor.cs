using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components.Inputs;
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
    [Inject]
    public required I18nService I18n { get; set; }

    private MembershipApplicationEntryState? EntryState;
    private MunicipalityPicker? _picker;

    protected override void OnInitialized() {
        EntryState = AppState.GetEntryState<MembershipApplicationEntryState>();
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
        var searched = _picker?.PostCodeQuery.Trim() ?? "";
        if (!string.IsNullOrEmpty(searched) && division.PostCodes != null && division.PostCodes.Contains(searched))
            return searched;
        if (!string.IsNullOrEmpty(division.PrimaryPostCode))
            return division.PrimaryPostCode;
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
