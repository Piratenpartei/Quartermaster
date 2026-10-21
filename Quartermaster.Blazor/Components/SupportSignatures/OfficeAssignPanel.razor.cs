using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

/// <summary>Gives a municipality its office: the one its Samtgemeinde already has, or a new one prefilled from the suggestion.</summary>
public partial class OfficeAssignPanel {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid MunicipalityId { get; set; }

    [Parameter]
    public EventCallback Assigned { get; set; }

    [Parameter]
    public EventCallback Cancelled { get; set; }

    private OfficeSuggestionDTO? Suggestion;
    private CertificationOfficeDTO? ExistingOffice;
    private CertificationOfficeSaveRequest Request = new();
    private bool Busy;
    private Guid LoadedMunicipalityId;

    protected override async Task OnParametersSetAsync() {
        if (MunicipalityId == LoadedMunicipalityId)
            return;

        LoadedMunicipalityId = MunicipalityId;
        Suggestion = await Signatures.SuggestOfficeAsync(MunicipalityId);
        if (Suggestion == null)
            return;

        if (Suggestion.ExistingOfficeId != null) {
            var offices = await Signatures.OfficesAsync() ?? [];
            ExistingOffice = offices.FirstOrDefault(o => o.Id == Suggestion.ExistingOfficeId);
        }
        Request = new CertificationOfficeSaveRequest {
            Name = Suggestion.SuggestedName,
            MunicipalityIds = Suggestion.Municipalities.Select(m => m.Id).ToList()
        };
    }

    private void Toggle(Guid municipalityId) {
        if (!Request.MunicipalityIds.Remove(municipalityId))
            Request.MunicipalityIds.Add(municipalityId);
    }

    private async Task AddToExisting() {
        if (ExistingOffice == null || Suggestion == null)
            return;

        var update = CertificationOfficeForm.ToRequest(ExistingOffice);
        update.MunicipalityIds = update.MunicipalityIds
            .Concat(Suggestion.Municipalities.Select(m => m.Id))
            .Distinct()
            .ToList();
        Busy = true;
        var saved = await Signatures.UpdateOfficeAsync(ExistingOffice.Id, update);
        Busy = false;
        if (saved != null)
            await Assigned.InvokeAsync();
    }

    private async Task CreateOffice() {
        Busy = true;
        var created = await Signatures.CreateOfficeAsync(Request);
        Busy = false;
        if (created != null)
            await Assigned.InvokeAsync();
    }
}
