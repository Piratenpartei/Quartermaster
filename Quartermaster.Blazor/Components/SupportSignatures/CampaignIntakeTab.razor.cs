using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

/// <summary>Fast entry of incoming forms: pick a municipality, enter the count, save, and the search is ready for the next batch.</summary>
public partial class CampaignIntakeTab {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Parameter, EditorRequired]
    public Guid CampaignId { get; set; }

    [Parameter]
    public EventCallback Changed { get; set; }

    private List<SignatureIntakeDTO>? Intakes;
    private AdministrativeDivisionDTO? Selected;
    private SignatureIntakeSaveRequest Draft = NewDraft();
    private SignatureIntakeSaveRequest? Editing;
    private ConfirmDialog DeleteConfirm = default!;
    private int PickerKey;
    private bool Busy;
    private bool FocusPicker;
    private Guid LoadedCampaignId;

    protected override async Task OnParametersSetAsync() {
        if (CampaignId == LoadedCampaignId)
            return;

        LoadedCampaignId = CampaignId;
        Intakes = await Signatures.IntakesAsync(CampaignId) ?? [];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!FocusPicker)
            return;

        FocusPicker = false;
        await JS.InvokeVoidAsync("qmFocus.firstInput", "intake-picker");
    }

    private static SignatureIntakeSaveRequest NewDraft() {
        return new SignatureIntakeSaveRequest { Count = 1, ReceivedOn = DateOnly.FromDateTime(DateTime.Now) };
    }

    private void Select(AdministrativeDivisionDTO municipality) {
        Selected = municipality;
    }

    private void ClearSelection() {
        Selected = null;
        PickerKey++;
        FocusPicker = true;
    }

    private async Task Save() {
        if (Selected == null)
            return;

        Draft.AdministrativeDivisionId = Selected.Id;
        Busy = true;
        var saved = await Signatures.AddIntakeAsync(CampaignId, Draft);
        Busy = false;
        if (saved == null)
            return;

        Draft = new SignatureIntakeSaveRequest { Count = 1, ReceivedOn = Draft.ReceivedOn, AlreadyCertified = Draft.AlreadyCertified };
        ClearSelection();
        Intakes = await Signatures.IntakesAsync(CampaignId) ?? Intakes;
        await Changed.InvokeAsync();
    }

    private void StartEdit(SignatureIntakeDTO intake) {
        Editing = new SignatureIntakeSaveRequest {
            Id = intake.Id,
            AdministrativeDivisionId = intake.AdministrativeDivisionId,
            Count = intake.Count,
            AlreadyCertified = intake.AlreadyCertified,
            ReceivedOn = intake.ReceivedOn,
            Note = intake.Note
        };
    }

    private async Task SaveEdit() {
        if (Editing == null)
            return;

        Busy = true;
        var saved = await Signatures.UpdateIntakeAsync(Editing.Id, Editing);
        Busy = false;
        if (saved == null)
            return;

        Editing = null;
        Intakes = await Signatures.IntakesAsync(CampaignId) ?? Intakes;
        await Changed.InvokeAsync();
    }

    private async Task Delete(SignatureIntakeDTO intake) {
        if (!await DeleteConfirm.ShowAsync())
            return;
        if (!await Signatures.DeleteIntakeAsync(intake.Id))
            return;

        Intakes = await Signatures.IntakesAsync(CampaignId) ?? Intakes;
        await Changed.InvokeAsync();
    }
}
