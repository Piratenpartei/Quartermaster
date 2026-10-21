using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.SupportSignatures;

/// <summary>
/// Sending forms to the offices: prepare shipments from the plan, print their cover letters, confirm them as sent, and record what comes back.
/// </summary>
public partial class CampaignShipmentsTab {
    [Inject]
    public required SupportSignatureService Signatures { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid CampaignId { get; set; }

    [Parameter, EditorRequired]
    public string CampaignName { get; set; } = "";

    [Parameter]
    public EventCallback Changed { get; set; }

    private ShipmentPlanDTO? Plan;
    private List<SignatureShipmentDTO>? Shipments;
    private readonly HashSet<Guid> SelectedOffices = [];
    private ConfirmDialog DiscardConfirm = default!;
    private Guid? SingleOfficeId;
    private List<EditableLine> SingleLines = [];
    private Guid? AssigningMunicipalityId;
    private Guid? ReturnFor;
    private Guid? EditingReturnId;
    private List<ReturnEdit> ReturnLines = [];
    private DateOnly SentOn = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly ReturnedOn = DateOnly.FromDateTime(DateTime.Now);
    private List<SignatureShipmentDTO> Prepared = [];
    private List<SignatureShipmentDTO> Sent = [];
    private bool Busy;
    private Guid LoadedCampaignId;

    protected override async Task OnParametersSetAsync() {
        if (CampaignId == LoadedCampaignId)
            return;

        LoadedCampaignId = CampaignId;
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Plan = await Signatures.PlanAsync(CampaignId);
        Shipments = await Signatures.ShipmentsAsync(CampaignId);
        Prepared = Shipments?.Where(s => s.SentOn == null).ToList() ?? [];
        Sent = Shipments?
            .Where(s => s.SentOn != null)
            .OrderByDescending(s => s.IsOverdue)
            .ThenByDescending(s => s.Open > 0)
            .ThenBy(s => s.SentOn)
            .ToList() ?? [];
        SelectedOffices.Clear();
        foreach (var office in Plan?.Offices ?? []) {
            SelectedOffices.Add(office.OfficeId);
        }
    }

    private async Task ReloadAsync() {
        await LoadAsync();
        await Changed.InvokeAsync();
    }

    private void ToggleOffice(Guid officeId) {
        if (!SelectedOffices.Remove(officeId))
            SelectedOffices.Add(officeId);
    }

    private void StartSingle(ShipmentPlanOfficeDTO office) {
        SingleOfficeId = office.OfficeId;
        SingleLines = office.Lines
            .Select(l => new EditableLine { MunicipalityId = l.MunicipalityId, Name = l.MunicipalityName, Max = l.Count, Count = l.Count })
            .ToList();
    }

    private async Task CreateSingle() {
        if (SingleOfficeId == null)
            return;

        Busy = true;
        var lines = SingleLines
            .Where(l => l.Count > 0)
            .Select(l => new PlanLineInput { MunicipalityId = l.MunicipalityId, Count = l.Count })
            .ToList();
        var created = await Signatures.CreateSingleAsync(CampaignId, SingleOfficeId.Value, lines);
        Busy = false;
        if (created == null)
            return;

        SingleOfficeId = null;
        await ReloadAsync();
    }

    private async Task OnOfficeAssigned() {
        AssigningMunicipalityId = null;
        await LoadAsync();
    }

    private async Task Prepare() {
        Busy = true;
        var prepared = await Signatures.PrepareAsync(CampaignId, SelectedOffices.ToList());
        Busy = false;
        if (prepared != null)
            await ReloadAsync();
    }

    private async Task PrintPrepared() {
        Busy = true;
        await Signatures.DownloadLettersAsync(CampaignId, Prepared.Select(s => s.Id), CampaignName);
        Busy = false;
    }

    private async Task ConfirmPrepared() {
        Busy = true;
        foreach (var shipment in Prepared) {
            if (!await Signatures.ConfirmAsync(shipment.Id, SentOn))
                break;
        }
        Busy = false;
        await ReloadAsync();
    }

    private async Task Discard(SignatureShipmentDTO shipment) {
        if (!await DiscardConfirm.ShowAsync())
            return;
        if (await Signatures.DiscardAsync(shipment.Id))
            await ReloadAsync();
    }

    private void StartReturn(SignatureShipmentDTO shipment) {
        ReturnFor = shipment.Id;
        EditingReturnId = null;
        ReturnedOn = DateOnly.FromDateTime(DateTime.Now);
        ReturnLines = shipment.Lines
            .Where(l => l.Open > 0)
            .Select(l => new ReturnEdit { MunicipalityId = l.MunicipalityId, Name = l.MunicipalityName, Open = l.Open, Certified = l.Open, Rejections = [new RejectionEdit()] })
            .ToList();
    }

    /// <summary>Fills the return form with a recorded return; its own forms count as open again, since saving replaces them.</summary>
    private void StartEditReturn(SignatureShipmentDTO shipment, SignatureReturnDTO signatureReturn) {
        ReturnFor = shipment.Id;
        EditingReturnId = signatureReturn.Id;
        ReturnedOn = signatureReturn.ReturnedOn;
        ReturnLines = shipment.Lines
            .Select(l => EditFor(l, signatureReturn.Lines.Where(r => r.MunicipalityId == l.MunicipalityId).ToList()))
            .Where(l => l.Open > 0)
            .ToList();
    }

    private static ReturnEdit EditFor(ShipmentLineDTO line, List<ReturnLineInput> own) {
        var rejections = own
            .Where(r => r.Rejected > 0)
            .GroupBy(r => r.RejectionReason)
            .Select(g => new RejectionEdit { Count = g.Sum(r => r.Rejected), Reason = g.Key?.ToString() ?? "" })
            .ToList();
        return new ReturnEdit {
            MunicipalityId = line.MunicipalityId,
            Name = line.MunicipalityName,
            Open = line.Open + own.Sum(r => r.Certified + r.Rejected),
            Certified = own.Sum(r => r.Certified),
            Rejections = rejections.Count > 0 ? rejections : [new RejectionEdit()]
        };
    }

    private static void AddRejection(ReturnEdit line) {
        line.Rejections.Add(new RejectionEdit());
    }

    private void CancelReturn() {
        ReturnFor = null;
        EditingReturnId = null;
    }

    private async Task SaveReturn() {
        if (ReturnFor == null)
            return;

        var request = new ReturnSaveRequest {
            ReturnedOn = ReturnedOn,
            Lines = ReturnLines
                .SelectMany(ToInputs)
                .ToList()
        };
        Busy = true;
        var saved = EditingReturnId == null
            ? await Signatures.RecordReturnAsync(ReturnFor.Value, request)
            : await Signatures.UpdateReturnAsync(EditingReturnId.Value, request);
        Busy = false;
        if (!saved)
            return;

        CancelReturn();
        await ReloadAsync();
    }

    /// <summary>One line for the certified forms and one per rejection reason, leaving out what is zero.</summary>
    private static IEnumerable<ReturnLineInput> ToInputs(ReturnEdit line) {
        if (line.Certified > 0)
            yield return new ReturnLineInput { MunicipalityId = line.MunicipalityId, Certified = line.Certified };

        foreach (var rejection in line.Rejections.Where(r => r.Count > 0)) {
            yield return new ReturnLineInput {
                MunicipalityId = line.MunicipalityId,
                Rejected = rejection.Count,
                RejectionReason = Enum.TryParse<SignatureRejectionReason>(rejection.Reason, out var reason) ? reason : null
            };
        }
    }

    private async Task DeleteReturn(Guid returnId) {
        if (!await DiscardConfirm.ShowAsync())
            return;
        if (await Signatures.DeleteReturnAsync(returnId))
            await ReloadAsync();
    }

    private sealed class EditableLine {
        public Guid MunicipalityId { get; set; }
        public string Name { get; set; } = "";
        public int Max { get; set; }
        public int Count { get; set; }
    }

    private sealed class ReturnEdit {
        public Guid MunicipalityId { get; set; }
        public string Name { get; set; } = "";
        public int Open { get; set; }
        public int Certified { get; set; }
        public List<RejectionEdit> Rejections { get; set; } = [];
    }

    private sealed class RejectionEdit {
        public int Count { get; set; }
        public string Reason { get; set; } = "";
    }
}
