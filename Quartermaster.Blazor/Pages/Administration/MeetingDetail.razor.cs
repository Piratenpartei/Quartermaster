using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.AuditLog;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Api;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required MeetingsApi MeetingsApi { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private DirtyForm _detailsForm = default!;
    private MeetingDetailDTO? Meeting;
    private bool Loading = true;
    private bool SavingMeta;
    private List<AuditLogDTO>? AuditLogs;
    private string ActiveTab = "agenda";
    private string? ProtocolHtml;
    private bool ProtocolLoading;

    private bool IsEditableMode =>
        Meeting?.Status == MeetingStatus.Draft || Meeting?.Status == MeetingStatus.Scheduled;

    private bool RequiresDraft =>
        Meeting?.Status != MeetingStatus.Completed && Meeting?.Status != MeetingStatus.Archived;

    protected override async Task OnInitializedAsync() {
        await LoadMeeting();
    }

    private async Task LoadMeeting() {
        Loading = true;
        try {
            Meeting = await MeetingsApi.GetAsync(Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        _detailsForm?.Reset();
        StateHasChanged();
    }

    private async Task OnTabChanged(string tab) {
        ActiveTab = tab;
        if (tab == "protocol")
            await LoadProtocol();
        if (tab == "audit")
            await LoadAuditLogs();
    }

    private async Task LoadProtocol() {
        if (Meeting == null)
            return;
        ProtocolLoading = true;
        StateHasChanged();
        try {
            ProtocolHtml = await MeetingsApi.GetProtocolHtmlAsync(Id, RequiresDraft);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        ProtocolLoading = false;
        StateHasChanged();
    }

    private async Task LoadAuditLogs() {
        try {
            AuditLogs = await Http.GetFromJsonAsync<List<AuditLogDTO>>($"/api/auditlog?entityType=Meeting&entityId={Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        StateHasChanged();
    }

    private void OnTitleChanged(string value) {
        if (Meeting != null)
            Meeting.Title = value;
    }

    private void OnDescriptionChanged(string value) {
        if (Meeting != null) {
            Meeting.Description = value;
            _detailsForm?.MarkDirty();
        }
    }

    private void OnDateChanged(string value) {
        if (Meeting == null)
            return;
        Meeting.MeetingDate = DateOnly.TryParse(value, out var d) ? d : null;
    }

    private void OnLocationChanged(string value) {
        if (Meeting != null)
            Meeting.Location = value;
    }

    private void OnVisibilityChanged(string value) {
        if (Meeting != null && int.TryParse(value, out var v))
            Meeting.Visibility = (MeetingVisibility)v;
    }

    private async Task SaveMeta() {
        if (Meeting == null)
            return;
        SavingMeta = true;
        StateHasChanged();
        try {
            await MeetingsApi.UpdateAsync(new MeetingUpdateRequest {
                Id = Id,
                Title = Meeting.Title,
                Visibility = Meeting.Visibility,
                MeetingDate = Meeting.MeetingDate,
                Location = Meeting.Location,
                Description = Meeting.Description
            });
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        SavingMeta = false;
        _detailsForm?.Reset();
        StateHasChanged();
    }

    private async Task ChangeStatus(MeetingStatus target) {
        if (Meeting == null)
            return;

        if (target == MeetingStatus.Scheduled && Meeting.Visibility == MeetingVisibility.Public) {
            if (!await ConfirmDialog.ShowAsync(I18n[I18nKey.Ui.MeetingDetail.PublicFinalizeConfirm]))
                return;
        }

        var confirm = target switch {
            MeetingStatus.Archived => I18n[I18nKey.Ui.MeetingDetail.ArchiveConfirm],
            MeetingStatus.Completed => I18n[I18nKey.Ui.MeetingDetail.CompleteConfirm],
            _ => null
        };
        if (confirm != null && !await ConfirmDialog.ShowAsync(confirm))
            return;

        try {
            var resp = await MeetingsApi.UpdateStatusAsync(new MeetingStatusUpdateRequest {
                Id = Id,
                Status = target
            });
            if (resp.IsSuccessStatusCode) {
                var targetLabel = MeetingStatusLabel(target);
                ToastService.Toast(I18n[$"{I18nKey.Ui.MeetingDetail.StatusChangedToast}?status={targetLabel}"], "success");
                await LoadMeeting();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private string MeetingStatusLabel(MeetingStatus status) => status switch {
        MeetingStatus.Draft => I18n[I18nKey.Ui.MeetingStatus.Draft],
        MeetingStatus.Scheduled => I18n[I18nKey.Ui.MeetingStatus.Scheduled],
        MeetingStatus.InProgress => I18n[I18nKey.Ui.MeetingStatus.InProgress],
        MeetingStatus.Completed => I18n[I18nKey.Ui.MeetingStatus.Completed],
        MeetingStatus.Archived => I18n[I18nKey.Ui.MeetingStatus.Archived],
        _ => status.ToString()
    };

    private async Task DeleteMeeting() {
        if (!await ConfirmDialog.ShowAsync(ToastService.Translate(I18nKey.Ui.Confirm.MeetingDelete)))
            return;
        try {
            await MeetingsApi.DeleteAsync(Id);
            ToastService.ToastKey(I18nKey.Ui.Toast.MeetingDeleted);
            Navigation.NavigateTo("/Administration/Meetings");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<(MeetingStatus Target, string Label, string Icon)> AllowedTransitions => Meeting?.Status switch {
        MeetingStatus.Draft => [
            (MeetingStatus.Scheduled, I18n[I18nKey.Ui.MeetingDetail.TransitionFinalize], "bi-calendar-check")
        ],
        MeetingStatus.Scheduled => [
            (MeetingStatus.Draft, I18n[I18nKey.Ui.MeetingDetail.TransitionBackToDraft], "bi-arrow-counterclockwise"),
            (MeetingStatus.InProgress, I18n[I18nKey.Ui.MeetingDetail.TransitionStart], "bi-play-circle")
        ],
        MeetingStatus.InProgress => [
            (MeetingStatus.Completed, I18n[I18nKey.Ui.MeetingDetail.TransitionComplete], "bi-check-circle")
        ],
        MeetingStatus.Completed => [
            (MeetingStatus.InProgress, I18n[I18nKey.Ui.MeetingDetail.TransitionBackToInProgress], "bi-arrow-counterclockwise"),
            (MeetingStatus.Archived, I18n[I18nKey.Ui.MeetingDetail.TransitionArchive], "bi-archive")
        ],
        MeetingStatus.Archived => [
            (MeetingStatus.Completed, I18n[I18nKey.Ui.MeetingDetail.TransitionUnarchive], "bi-box-arrow-up")
        ],
        _ => new List<(MeetingStatus, string, string)>()
    };

}
