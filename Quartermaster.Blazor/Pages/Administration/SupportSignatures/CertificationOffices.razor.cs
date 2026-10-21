using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
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

    private List<CertificationOfficeDTO>? Offices;
    private CertificationOfficeSaveRequest? Editing;
    private List<OfficeMunicipalityDTO> EditingMunicipalities = [];
    private ConfirmDialog DeleteConfirm = default!;
    private int PickerKey;
    private bool Busy;

    protected override async Task OnInitializedAsync() {
        Offices = await Signatures.OfficesAsync() ?? [];
    }

    private void StartCreate() {
        Editing = new CertificationOfficeSaveRequest();
        EditingMunicipalities = [];
    }

    private void StartEdit(CertificationOfficeDTO office) {
        Editing = CertificationOfficeForm.ToRequest(office);
        EditingMunicipalities = [.. office.Municipalities];
    }

    private void AddMunicipality(AdministrativeDivisionDTO municipality) {
        if (EditingMunicipalities.All(m => m.Id != municipality.Id))
            EditingMunicipalities.Add(new OfficeMunicipalityDTO { Id = municipality.Id, Name = municipality.Name });
        PickerKey++;
    }

    private void RemoveMunicipality(Guid municipalityId) {
        EditingMunicipalities.RemoveAll(m => m.Id == municipalityId);
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
        Offices = await Signatures.OfficesAsync() ?? Offices;
    }

    private async Task Delete(CertificationOfficeDTO office) {
        if (!await DeleteConfirm.ShowAsync())
            return;
        if (await Signatures.DeleteOfficeAsync(office.Id))
            Offices = await Signatures.OfficesAsync() ?? Offices;
    }
}
