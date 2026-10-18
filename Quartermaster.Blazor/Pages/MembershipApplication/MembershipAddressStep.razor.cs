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

/// <summary>Germany or abroad first; in Germany the municipality search fills post code, city and chapter before the street is asked.</summary>
public partial class MembershipAddressStep {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MembershipApplicationEntryState State { get; set; } = default!;

    private MunicipalityPicker? _picker;

    private bool ShowMunicipalitySearch() {
        return State.IsGermany && State.AddressAdministrativeDivisionId == null && !State.ManualAddressEntry;
    }

    private bool FromMunicipality() {
        return State.AddressAdministrativeDivisionId != null;
    }

    private void SelectCountry(bool isGermany) {
        if (State.CountrySelected && State.IsGermany == isGermany)
            return;

        State.SelectCountry(isGermany, I18n[I18nKey.Ui.CountrySelection.GermanyCountryName]);
    }

    private async Task SelectDivision(AdministrativeDivisionDTO division) {
        State.AddressAdministrativeDivisionId = division.Id;
        State.AddressCity = division.Name;
        State.AddressPostCode = ResolvePostCode(division);
        await LookupChapter(division.Id);
    }

    private void EnterManually() {
        State.ClearMunicipality();
        State.ManualAddressEntry = true;
    }

    private void SearchAgain() {
        State.ClearMunicipality();
        State.ManualAddressEntry = false;
    }

    // The searched post code wins when the municipality has it, so applicants in multi-post-code towns keep theirs.
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
        State.ChapterId = null;
        State.ChapterName = null;
        try {
            var response = await Http.GetAsync($"/api/chapters/for-division/{divisionId}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return;

            response.EnsureSuccessStatusCode();
            var chapter = await response.Content.ReadFromJsonAsync<ChapterDTO>();
            if (chapter != null) {
                State.ChapterId = chapter.Id;
                State.ChapterName = chapter.Name;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
