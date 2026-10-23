using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Components.SupportSignatures;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.SupportSignatures;

public partial class CertificationOffices {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    private List<CertificationOfficeDTO>? Offices;
    private CertificationOfficeSaveRequest? Editing;
    private List<OfficeMunicipalityDTO> EditingMunicipalities = [];
    private bool PrefilledFromOfficial;
    private CertificationOfficeDTO? ResponsibleOffice;
    private OfficeSuggestionDTO? ResponsibleSuggestion;
    private string ResponsibleMunicipalityName = "";
    private AdministrativeDivisionDTO? LookupMunicipality;
    private CertificationOfficeDTO? LookupOffice;
    private bool ImportOpen;
    private byte[]? ImportContent;
    private OfficeImportResultDTO? ImportResult;
    private ConfirmDialog DeleteConfirm = default!;
    private int PickerKey;
    private int LookupPickerKey;
    private bool Busy;

    protected override async Task OnInitializedAsync() {
        Offices = await Signatures.OfficesAsync() ?? [];
    }

    private void StartCreate() {
        Editing = new CertificationOfficeSaveRequest();
        EditingMunicipalities = [];
        PrefilledFromOfficial = false;
        ResponsibleOffice = null;
    }

    private void StartEdit(CertificationOfficeDTO office) {
        Editing = CertificationOfficeForm.ToRequest(office);
        EditingMunicipalities = [.. office.Municipalities];
        PrefilledFromOfficial = false;
        ResponsibleOffice = null;
    }

    private async Task AddMunicipality(AdministrativeDivisionDTO municipality) {
        PickerKey++;
        ResponsibleOffice = null;
        if (EditingMunicipalities.Any(m => m.Id == municipality.Id))
            return;

        if (!IsFirstPickOfNewOffice()) {
            EditingMunicipalities.Add(new OfficeMunicipalityDTO { Id = municipality.Id, Name = municipality.Name });
            return;
        }

        var suggestion = await Signatures.SuggestOfficeAsync(municipality.Id);
        if (suggestion == null) {
            EditingMunicipalities.Add(new OfficeMunicipalityDTO { Id = municipality.Id, Name = municipality.Name });
            return;
        }

        var existing = Offices?.FirstOrDefault(o => o.Id == suggestion.ExistingOfficeId);
        if (existing != null) {
            ResponsibleOffice = existing;
            ResponsibleSuggestion = suggestion;
            ResponsibleMunicipalityName = municipality.Name;
            return;
        }

        Prefill(Editing!, suggestion);
        PrefilledFromOfficial = suggestion.FromOfficialDirectory;
        EditingMunicipalities.AddRange(suggestion.Municipalities);
    }

    /// <summary>Fills the fields still empty, so nothing typed before picking the municipality gets lost.</summary>
    private static void Prefill(CertificationOfficeSaveRequest request, OfficeSuggestionDTO suggestion) {
        if (string.IsNullOrWhiteSpace(request.Name))
            request.Name = suggestion.SuggestedName;
        if (string.IsNullOrWhiteSpace(request.Street))
            request.Street = suggestion.Street;
        if (string.IsNullOrWhiteSpace(request.PostCode))
            request.PostCode = suggestion.PostCode;
        if (string.IsNullOrWhiteSpace(request.City))
            request.City = suggestion.City;
        if (string.IsNullOrWhiteSpace(request.Email))
            request.Email = suggestion.Email;
    }

    private bool IsFirstPickOfNewOffice() {
        if (Editing == null)
            return false;
        if (Editing.Id != Guid.Empty)
            return false;

        return EditingMunicipalities.Count == 0;
    }

    /// <summary>Opens the office that already certifies for the picked municipality, with the municipalities of the suggestion added.</summary>
    private void EditResponsibleOffice() {
        if (ResponsibleOffice == null || ResponsibleSuggestion == null)
            return;

        var suggested = ResponsibleSuggestion.Municipalities;
        StartEdit(ResponsibleOffice);
        EditingMunicipalities.AddRange(suggested.Where(s => EditingMunicipalities.All(m => m.Id != s.Id)));
    }

    private void RemoveMunicipality(Guid municipalityId) {
        EditingMunicipalities.RemoveAll(m => m.Id == municipalityId);
    }

    private async Task Lookup(AdministrativeDivisionDTO municipality) {
        LookupPickerKey++;
        LookupMunicipality = municipality;
        LookupOffice = await Signatures.ResponsibleOfficeAsync(municipality.Id);
    }

    private async Task CreateFromLookup() {
        if (LookupMunicipality == null)
            return;

        StartCreate();
        await AddMunicipality(LookupMunicipality);
    }

    private void EditFromLookup() {
        var office = Offices?.FirstOrDefault(o => o.Id == LookupOffice?.Id);
        if (office != null)
            StartEdit(office);
    }

    private async Task Save() {
        if (Editing == null)
            return;

        Editing.MunicipalityIds = EditingMunicipalities.Select(m => m.Id).ToList();
        Busy = true;
        var saved = Editing.Id == Guid.Empty
            ? await Signatures.CreateOfficeAsync(Editing)
            : await Signatures.UpdateOfficeAsync(Editing.Id, Editing);
        Busy = false;
        if (saved == null)
            return;

        Editing = null;
        await Reload();
    }

    private async Task Delete(CertificationOfficeDTO office) {
        if (!await DeleteConfirm.ShowAsync())
            return;
        if (await Signatures.DeleteOfficeAsync(office.Id))
            await Reload();
    }

    private void OpenImport() {
        ImportOpen = true;
        ImportContent = null;
        ImportResult = null;
    }

    private void CloseImport() {
        ImportOpen = false;
        ImportContent = null;
        ImportResult = null;
    }

    private async Task SelectImportFile(InputFileChangeEventArgs args) {
        ImportResult = null;
        if (args.File.Size > OfficeImportRequest.MaxContentBytes) {
            ToastService.ErrorKey(I18nKey.Error.SupportSignatures.OfficeImport.FileTooLarge);
            return;
        }

        Busy = true;
        using var buffer = new MemoryStream();
        await using (var stream = args.File.OpenReadStream(OfficeImportRequest.MaxContentBytes)) {
            await stream.CopyToAsync(buffer);
        }
        ImportContent = buffer.ToArray();
        ImportResult = await Signatures.ImportOfficesAsync(ImportContent, false);
        Busy = false;
    }

    private async Task ApplyImport() {
        if (ImportContent == null)
            return;

        Busy = true;
        var result = await Signatures.ImportOfficesAsync(ImportContent, true);
        Busy = false;
        if (result == null)
            return;

        ToastService.ToastKey(I18nKey.Ui.SupportSignatures.ImportApplied);
        CloseImport();
        await Reload();
    }

    private async Task Reload() {
        Offices = await Signatures.OfficesAsync() ?? Offices;
        if (LookupMunicipality != null)
            LookupOffice = await Signatures.ResponsibleOfficeAsync(LookupMunicipality.Id);
    }
}
