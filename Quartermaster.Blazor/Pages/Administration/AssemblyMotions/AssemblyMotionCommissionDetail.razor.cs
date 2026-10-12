using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.AssemblyMotions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.AssemblyMotions;

public partial class AssemblyMotionCommissionDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private AssemblyMotionDetailDTO? Detail;
    private AssemblyMotionPortalDTO? Portal;
    private AssemblyMotionFieldValues Fields = new();
    private ConfirmDialog _withdrawDialog = default!;
    private ConfirmDialog _reloadDialog = default!;
    private bool Loading = true;
    private bool Busy;
    private bool Editing;
    private string? Conflict;

    // The revision the edit form was filled from. Detail can move on to a newer revision while the form is open
    // (check, inquiry or withdraw responses), and saving against that would overwrite someone else's edit unseen.
    private int editBaseRevision;
    private string ChangeNote = "";
    private AssemblyMotionStatus CheckStatus = AssemblyMotionStatus.Submitted;
    private string CheckNote = "";
    private string ResultSelection = "0";

    private string BackHref => Detail == null
        ? "/Administration/AssemblyMotionPortals"
        : $"/Administration/Events/{Detail.EventId}/Motions";

    private List<AssemblyMotionTypeDTO> PortalTypes => Portal?.Types ?? [];

    private bool ResultsEnabled => Portal != null && AssemblyMotionDisplay.IsEventReached(Portal.EventDate);

    private string ResultsLockedText => AssemblyMotionDisplay.ResultsLockedText(I18n, Portal?.EventDate);

    private bool CanCheck => Detail != null && IsCheckStatus(Detail.Status);

    private bool CanCorrect => Detail != null && IsCheckStatus(Detail.Status) && !string.IsNullOrEmpty(Detail.DisplayNumber);

    private bool CanCommissionWithdraw => Detail != null
        && Detail.Status != AssemblyMotionStatus.Draft
        && Detail.Status != AssemblyMotionStatus.Withdrawn
        && Detail.Status != AssemblyMotionStatus.Lapsed;

    private bool CanAsk => CanCommissionWithdraw && !string.IsNullOrEmpty(Detail?.DisplayNumber);

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    private static bool IsCheckStatus(AssemblyMotionStatus status)
        => status == AssemblyMotionStatus.Submitted
            || status == AssemblyMotionStatus.Admitted
            || status == AssemblyMotionStatus.FormallyInsufficient;

    private bool IsNotCheckStatus(AssemblyMotionStatus status) => !IsCheckStatus(status);

    private string CheckStatusLabel(AssemblyMotionStatus status) => AssemblyMotionDisplay.StatusLabel(I18n, status, false);

    private async Task LoadAsync() {
        Loading = true;
        Conflict = null;
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/motions/{Id}");
            Detail = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>() : null;
            if (Detail != null) {
                FillEditForm(Detail);
                ResetCheckForm(Detail);
                ResultSelection = ((int)Detail.Result).ToString();
                var portalResponse = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{Detail.EventId}");
                Portal = portalResponse.IsSuccessStatusCode ? await portalResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private void Apply(AssemblyMotionDetailDTO detail) {
        Detail = detail;
        if (!Editing)
            FillEditForm(detail);
    }

    private void FillEditForm(AssemblyMotionDetailDTO detail) {
        Fields.Fill(detail);
        editBaseRevision = detail.Revision;
    }

    private void ResetCheckForm(AssemblyMotionDetailDTO detail) {
        CheckStatus = IsCheckStatus(detail.Status) ? detail.Status : AssemblyMotionStatus.Submitted;
        CheckNote = detail.CheckNote ?? "";
    }

    private void ToggleEdit() {
        Editing = !Editing;
        Conflict = null;
        if (!Editing && Detail != null)
            FillEditForm(Detail);
    }

    private async Task ReloadAsync() {
        if (!await _reloadDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.ReloadConfirmMessage]))
            return;

        await LoadAsync();
    }

    private async Task SaveCheck() {
        if (CheckStatus == AssemblyMotionStatus.FormallyInsufficient && string.IsNullOrWhiteSpace(CheckNote)) {
            ToastService.ErrorKey(I18nKey.Ui.AssemblyMotion.CheckNoteRequired);
            return;
        }

        var request = new AssemblyMotionCheckRequest {
            Id = Id,
            Status = CheckStatus,
            CheckNote = string.IsNullOrWhiteSpace(CheckNote) ? null : CheckNote
        };
        if (await RunAsync(() => Http.PostAsJsonAsync($"/api/assemblymotions/motions/{Id}/check", request), I18nKey.Ui.AssemblyMotion.ToastChecked) && Detail != null)
            ResetCheckForm(Detail);
    }

    private string ResultOptionLabel(AssemblyMotionResult result) => AssemblyMotionDisplay.ResultOptionLabel(I18n, result);

    private async Task SaveResult() {
        if (!ResultsEnabled || !int.TryParse(ResultSelection, out var value))
            return;

        var request = new AssemblyMotionResultRequest {
            Id = Id,
            Result = (AssemblyMotionResult)value
        };
        if (await RunAsync(() => Http.PutAsJsonAsync($"/api/assemblymotions/motions/{Id}/result", request), I18nKey.Ui.AssemblyMotion.ToastResultSaved) && Detail != null)
            ResultSelection = ((int)Detail.Result).ToString();
    }

    private async Task SaveEdit() {
        if (Detail == null || Portal == null)
            return;
        if (string.IsNullOrWhiteSpace(ChangeNote)) {
            ToastService.ErrorKey(I18nKey.Ui.AssemblyMotion.ChangeNoteRequired);
            return;
        }

        var request = new AssemblyMotionCommissionEditRequest {
            PortalId = Portal.Id,
            Id = Id,
            BaseRevision = editBaseRevision,
            ChangeNote = ChangeNote
        };
        if (!Fields.TryApplyTo(request))
            return;

        Conflict = null;
        if (await RunAsync(() => Http.PutAsJsonAsync($"/api/assemblymotions/motions/{Id}/commission-edit", request), I18nKey.Ui.AssemblyMotion.ToastCommissionEdited)) {
            Editing = false;
            ChangeNote = "";
            if (Detail != null)
                FillEditForm(Detail);
        }
    }

    private async Task CommissionWithdraw() {
        if (!await _withdrawDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.CommissionWithdrawMessage]))
            return;

        await RunAsync(() => Http.PostAsJsonAsync($"/api/assemblymotions/motions/{Id}/commission-withdraw", new { }), I18nKey.Ui.AssemblyMotion.ToastCommissionWithdrawn);
    }

    private async Task<bool> RunAsync(Func<Task<HttpResponseMessage>> send, string toastKey) {
        Busy = true;
        var succeeded = false;
        try {
            var response = await send();
            if (response.IsSuccessStatusCode) {
                var updated = await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
                if (updated != null)
                    Apply(updated);
                ToastService.ToastKey(toastKey);
                succeeded = true;
            } else {
                await ShowErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
        return succeeded;
    }

    private async Task ShowErrorAsync(HttpResponseMessage response) {
        Conflict = await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
    }
}
