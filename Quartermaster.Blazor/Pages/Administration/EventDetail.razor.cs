using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AuditLog;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventDetail {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private DirtyForm _detailsForm = default!;
    private EventDetailDTO? Event;
    private bool Loading = true;
    private bool EditingTitle;
    private List<AuditLogDTO>? AuditLogs;
    private bool AuditUnavailable;

    private bool ReadOnly => Event?.CanEdit != true;

    protected override async Task OnInitializedAsync() {
        await LoadEvent();
    }

    private async Task LoadEvent() {
        Loading = true;
        try {
            Event = await Http.GetFromJsonAsync<EventDetailDTO>($"/api/events/{Id}");
            if (Event != null && !Event.CanManage) {
                Navigation.NavigateTo($"/Events/{Id}", replace: true);
                return;
            }
            var auditResponse = await Http.GetAsync($"/api/auditlog?entityType=Event&entityId={Id}");
            AuditUnavailable = !auditResponse.IsSuccessStatusCode;
            if (!AuditUnavailable)
                AuditLogs = await auditResponse.Content.ReadFromJsonAsync<List<AuditLogDTO>>();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        _detailsForm?.Reset();
        StateHasChanged();
    }

    private void OnDescriptionChanged(string value) {
        if (Event != null) {
            Event.Description = value;
            _detailsForm?.MarkDirty();
        }
    }

    private void OnDateChanged(string value) {
        if (Event != null) {
            Event.EventDate = DateOnly.TryParse(value, out var d) ? d : null;
        }
    }

    private void OnStartTimeChanged(string value) {
        if (Event != null) {
            Event.StartTime = TimeOnly.TryParse(value, out var t) ? t : null;
            _detailsForm?.MarkDirty();
        }
    }

    private void OnMeetingUrlChanged(string value) {
        if (Event != null) {
            Event.MeetingUrl = value;
            _detailsForm?.MarkDirty();
        }
    }

    private void OnVisibilityChanged(string value) {
        if (Event != null && int.TryParse(value, out var v)) {
            Event.Visibility = (EventVisibility)v;
            _detailsForm?.MarkDirty();
        }
    }

    private async Task<bool> SaveDetails() {
        if (Event == null)
            return false;

        try {
            var response = await Http.PutAsJsonAsync($"/api/events/{Id}", UpdateRequest(Event));
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return false;
            }
            _detailsForm.Reset();
            return true;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            return false;
        }
    }

    private EventUpdateRequest UpdateRequest(EventDetailDTO ev) {
        return new EventUpdateRequest {
            Id = Id,
            InternalName = ev.InternalName,
            PublicName = ev.PublicName,
            Description = ev.Description,
            EventDate = ev.EventDate,
            StartTime = ev.StartTime,
            MeetingUrl = ev.MeetingUrl,
            Visibility = ev.Visibility
        };
    }

    private async Task SaveTitleEdit() {
        EditingTitle = false;
        _detailsForm.MarkDirty();
        if (await SaveDetails())
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
    }

    private async Task SaveIfDirty() {
        if (_detailsForm.IsDirty && Event != null) {
            try {
                var response = await Http.PutAsJsonAsync($"/api/events/{Id}", UpdateRequest(Event));
                if (response.IsSuccessStatusCode) {
                    _detailsForm.Reset();
                } else {
                    await ToastService.ErrorAsync(response);
                }
            } catch (HttpRequestException ex) {
                ToastService.Error(ex);
            }
        }
    }

    private async Task ChangeStatus(EventStatus target) {
        if (Event == null)
            return;

        var confirmKey = target switch {
            EventStatus.Archived => I18nKey.Ui.Confirm.EventArchive,
            EventStatus.Draft => I18nKey.Ui.Confirm.EventBackToDraft,
            _ => null
        };

        if (confirmKey != null && !await ConfirmDialog.ShowAsync(ToastService.Translate(confirmKey)))
            return;

        try {
            await SaveIfDirty();
            var response = await Http.PutAsJsonAsync($"/api/events/{Id}/status",
                new EventStatusUpdateRequest { Id = Id, Status = target });
            if (response.IsSuccessStatusCode) {
                var labelKey = target switch {
                    EventStatus.Draft => I18nKey.Ui.Label.EventStatusDraft,
                    EventStatus.Active => I18nKey.Ui.Label.EventStatusActive,
                    EventStatus.Completed => I18nKey.Ui.Label.EventStatusCompleted,
                    EventStatus.Archived => I18nKey.Ui.Label.EventStatusArchived,
                    _ => null
                };
                var statusLabel = labelKey != null ? ToastService.Translate(labelKey) : target.ToString();
                ToastService.Toast(
                    ToastService.Translate(I18nParams.With(I18nKey.Ui.Toast.EventStatusChanged, ("status", statusLabel))),
                    "success");
                await LoadEvent();
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<(EventStatus Target, string LabelKey, string Icon)> AllowedTransitions {
        get {
            if (Event == null)
                return [];
            return TransitionsFrom(Event.Status)
                .Where(t => CanChangeStatus(Event, t.Target))
                .ToList();
        }
    }

    /// <summary>Mirrors <c>EventStatusUpdateEndpoint</c>: anything into or out of Archived needs DeleteEvents.</summary>
    private static bool CanChangeStatus(EventDetailDTO ev, EventStatus target) {
        if (target == EventStatus.Archived || ev.Status == EventStatus.Archived)
            return ev.CanArchive;
        return ev.CanEdit;
    }

    private static List<(EventStatus Target, string LabelKey, string Icon)> TransitionsFrom(EventStatus status) {
        return status switch {
            EventStatus.Draft => [(EventStatus.Active, I18nKey.Ui.EventDetail.TransitionActivate, "bi-play-circle")],
            EventStatus.Active => [
                (EventStatus.Draft, I18nKey.Ui.EventDetail.TransitionBackToDraft, "bi-arrow-counterclockwise"),
                (EventStatus.Completed, I18nKey.Ui.EventDetail.TransitionMarkCompleted, "bi-check-circle")
            ],
            EventStatus.Completed => [
                (EventStatus.Active, I18nKey.Ui.EventDetail.TransitionBackToActive, "bi-arrow-counterclockwise"),
                (EventStatus.Archived, I18nKey.Ui.EventDetail.TransitionArchive, "bi-archive")
            ],
            EventStatus.Archived => [(EventStatus.Completed, I18nKey.Ui.EventDetail.TransitionUnarchive, "bi-box-arrow-up")],
            _ => []
        };
    }
}
