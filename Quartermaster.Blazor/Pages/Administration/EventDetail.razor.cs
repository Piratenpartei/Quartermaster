using System;
using System.Collections.Generic;
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
    private bool Saving;
    private bool EditingTitle;
    private List<AuditLogDTO>? AuditLogs;

    protected override async Task OnInitializedAsync() {
        await LoadEvent();
    }

    private async Task LoadEvent() {
        Loading = true;
        try {
            Event = await Http.GetFromJsonAsync<EventDetailDTO>($"/api/events/{Id}");
            AuditLogs = await Http.GetFromJsonAsync<List<AuditLogDTO>>($"/api/auditlog?entityType=Event&entityId={Id}");
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

    private void OnVisibilityChanged(string value) {
        if (Event != null && int.TryParse(value, out var v)) {
            Event.Visibility = (EventVisibility)v;
            _detailsForm?.MarkDirty();
        }
    }

    private async Task SaveDetails() {
        if (Event == null)
            return;

        Saving = true;
        StateHasChanged();

        try {
            await Http.PutAsJsonAsync($"/api/events/{Id}", new EventUpdateRequest {
                Id = Id,
                InternalName = Event.InternalName,
                PublicName = Event.PublicName,
                Description = Event.Description,
                EventDate = Event.EventDate,
                Visibility = Event.Visibility
            });
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Saving = false;
        _detailsForm.Reset();
        StateHasChanged();
    }

    private async Task SaveTitleEdit() {
        EditingTitle = false;
        _detailsForm.MarkDirty();
        await SaveDetails();
    }

    private async Task SaveIfDirty() {
        if (_detailsForm.IsDirty && Event != null) {
            try {
                await Http.PutAsJsonAsync($"/api/events/{Id}", new EventUpdateRequest {
                    Id = Id,
                    InternalName = Event.InternalName,
                    PublicName = Event.PublicName,
                    Description = Event.Description,
                    EventDate = Event.EventDate
                });
                _detailsForm.Reset();
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

    private List<(EventStatus Target, string LabelKey, string Icon)> AllowedTransitions => Event?.Status switch {
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
