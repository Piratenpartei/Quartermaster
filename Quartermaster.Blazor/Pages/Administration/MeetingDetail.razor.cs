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
        Meeting.MeetingDate = DateTime.TryParse(value, out var d) ? d : null;
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

        // Finalisieren: confirm dialog for Public meetings
        if (target == MeetingStatus.Scheduled && Meeting.Visibility == MeetingVisibility.Public) {
            if (!await ConfirmDialog.ShowAsync(
                "Diese Sitzung ist als öffentlich markiert. Nach der Finalisierung wird sie für alle sichtbar. Fortfahren?"))
                return;
        }

        var confirm = target switch {
            MeetingStatus.Archived => "Diese Sitzung wirklich archivieren? Ein PDF-Snapshot wird erstellt.",
            MeetingStatus.Completed => "Sitzung wirklich abschließen?",
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
                var targetLabel = target switch {
                    MeetingStatus.Draft => "Entwurf",
                    MeetingStatus.Scheduled => "Geplant",
                    MeetingStatus.InProgress => "Laufend",
                    MeetingStatus.Completed => "Abgeschlossen",
                    MeetingStatus.Archived => "Archiviert",
                    _ => target.ToString()
                };
                ToastService.Toast($"Status geändert: {targetLabel}.", "success");
                await LoadMeeting();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

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
        MeetingStatus.Draft => [(MeetingStatus.Scheduled, "Finalisieren", "bi-calendar-check")],
        MeetingStatus.Scheduled => [
            (MeetingStatus.Draft, "Zurück zu Entwurf", "bi-arrow-counterclockwise"),
            (MeetingStatus.InProgress, "Sitzung starten", "bi-play-circle")
        ],
        MeetingStatus.InProgress => [(MeetingStatus.Completed, "Sitzung abschließen", "bi-check-circle")],
        MeetingStatus.Completed => [
            (MeetingStatus.InProgress, "Zurück zu Laufend", "bi-arrow-counterclockwise"),
            (MeetingStatus.Archived, "Archivieren", "bi-archive")
        ],
        MeetingStatus.Archived => [(MeetingStatus.Completed, "Dearchivieren", "bi-box-arrow-up")],
        _ => new List<(MeetingStatus, string, string)>()
    };

}
