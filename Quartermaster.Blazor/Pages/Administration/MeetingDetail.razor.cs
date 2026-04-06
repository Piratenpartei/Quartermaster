using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AuditLog;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingDetail : IDisposable {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private MeetingDetailDTO? Meeting;
    private bool Loading = true;
    private bool SavingMeta;
    private List<AuditLogDTO>? AuditLogs;
    private string ActiveTab = "agenda";
    private string? ProtocolHtml;
    private bool ProtocolLoading;

    // Live minute-taking state
    private Guid? SelectedActiveItemId;
    private string ActiveItemNotes = "";
    private string ActiveItemResolution = "";
    private bool NotesSaving;
    private DateTime? NotesSavedAt;
    private Timer? _notesDebounce;
    private bool _notesDirty;

    private Guid? CurrentUserId => AuthService.CurrentUser?.Id;

    private bool IsEditableMode =>
        Meeting?.Status == MeetingStatus.Draft || Meeting?.Status == MeetingStatus.Scheduled;

    private bool RequiresDraft =>
        Meeting?.Status != MeetingStatus.Completed && Meeting?.Status != MeetingStatus.Archived;

    private AgendaItemDTO? SelectedActiveItem =>
        SelectedActiveItemId == null
            ? null
            : Meeting?.AgendaItems.FirstOrDefault(a => a.Id == SelectedActiveItemId);

    protected override async Task OnInitializedAsync() {
        await LoadMeeting();
    }

    public void Dispose() {
        _notesDebounce?.Dispose();
    }

    private async Task LoadMeeting() {
        Loading = true;
        try {
            Meeting = await Http.GetFromJsonAsync<MeetingDetailDTO>($"/api/meetings/{Id}");
            if (Meeting != null && SelectedActiveItemId == null && Meeting.Status == MeetingStatus.InProgress) {
                // Auto-select the in-progress item or the first not-started one
                var inProgress = Meeting.AgendaItems.FirstOrDefault(a => a.StartedAt != null && a.CompletedAt == null);
                SelectedActiveItemId = inProgress?.Id ?? Meeting.AgendaItems.OrderBy(a => a.SortOrder).FirstOrDefault()?.Id;
                LoadActiveItemFields();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private void LoadActiveItemFields() {
        var active = SelectedActiveItem;
        ActiveItemNotes = active?.Notes ?? "";
        ActiveItemResolution = active?.Resolution ?? "";
        NotesSavedAt = null;
        _notesDirty = false;
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
            var draft = RequiresDraft ? "&draft=true" : "";
            ProtocolHtml = await Http.GetStringAsync($"/api/meetings/{Id}/protocol?format=html{draft}");
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

    private void OnDescriptionChanged(string value) {
        if (Meeting != null)
            Meeting.Description = value;
    }

    private void OnDateChanged(ChangeEventArgs e) {
        if (Meeting == null)
            return;
        var val = e.Value?.ToString() ?? "";
        Meeting.MeetingDate = DateTime.TryParse(val, out var d) ? d : null;
    }

    private void OnVisibilityChanged(ChangeEventArgs e) {
        if (Meeting == null)
            return;
        if (int.TryParse(e.Value?.ToString(), out var v))
            Meeting.Visibility = (MeetingVisibility)v;
    }

    private async Task SaveMeta() {
        if (Meeting == null)
            return;
        SavingMeta = true;
        StateHasChanged();
        try {
            await Http.PutAsJsonAsync($"/api/meetings/{Id}", new MeetingUpdateRequest {
                Id = Id,
                Title = Meeting.Title,
                Visibility = Meeting.Visibility,
                MeetingDate = Meeting.MeetingDate,
                Location = Meeting.Location,
                Description = Meeting.Description
            });
            ToastService.Toast("Gespeichert.", "success");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        SavingMeta = false;
        StateHasChanged();
    }

    private Task AddAgendaItemChild(Guid parentId) => AddAgendaItem(parentId);

    private async Task AddAgendaItem(Guid? parentId) {
        if (Meeting == null)
            return;
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda", new AgendaItemCreateRequest {
                MeetingId = Id,
                ParentId = parentId,
                Title = "Neuer TOP",
                ItemType = AgendaItemType.Discussion
            });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task DeleteAgendaItem(Guid itemId) {
        if (!await ConfirmDialog.ShowAsync("Diesen Tagesordnungspunkt wirklich löschen?"))
            return;
        try {
            await Http.DeleteAsync($"/api/meetings/{Id}/agenda/{itemId}");
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReorderAgendaItem((Guid ItemId, int Direction) args) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{args.ItemId}/reorder",
                new AgendaItemReorderRequest {
                    MeetingId = Id,
                    ItemId = args.ItemId,
                    Direction = args.Direction
                });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task UpdateAgendaItem(AgendaItemUpdatePayload payload) {
        if (Meeting == null)
            return;
        var existing = Meeting.AgendaItems.FirstOrDefault(a => a.Id == payload.ItemId);
        try {
            await Http.PutAsJsonAsync($"/api/meetings/{Id}/agenda/{payload.ItemId}",
                new AgendaItemUpdateRequest {
                    MeetingId = Id,
                    ItemId = payload.ItemId,
                    Title = payload.Title,
                    ItemType = payload.ItemType,
                    MotionId = payload.MotionId,
                    Notes = existing?.Notes,
                    Resolution = existing?.Resolution
                });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void OnSelectActiveItem(Guid itemId) {
        SelectedActiveItemId = itemId;
        LoadActiveItemFields();
        StateHasChanged();
    }

    private async Task StartAgendaItem(Guid itemId) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/start", new { });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CompleteAgendaItem(Guid itemId) {
        try {
            // Save pending notes/resolution first
            await FlushNotes();
            await SaveActiveItemResolution();
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/complete", new { });
            // Advance to next item
            var advanced = AdvanceToNextItem(itemId);
            await LoadMeeting();
            if (advanced)
                LoadActiveItemFields();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private bool AdvanceToNextItem(Guid completedId) {
        if (Meeting == null)
            return false;
        var ordered = Meeting.AgendaItems.OrderBy(a => a.ParentId ?? Guid.Empty).ThenBy(a => a.SortOrder).ToList();
        var idx = ordered.FindIndex(a => a.Id == completedId);
        if (idx >= 0 && idx + 1 < ordered.Count) {
            SelectedActiveItemId = ordered[idx + 1].Id;
            return true;
        }
        return false;
    }

    private void OnNotesInput(ChangeEventArgs e) {
        ActiveItemNotes = e.Value?.ToString() ?? "";
        _notesDirty = true;
        _notesDebounce?.Dispose();
        _notesDebounce = new Timer(_ => InvokeAsync(FlushNotes), null, 3000, Timeout.Infinite);
    }

    private async Task FlushNotes() {
        if (!_notesDirty || SelectedActiveItemId == null)
            return;
        NotesSaving = true;
        StateHasChanged();
        try {
            await Http.PutAsJsonAsync($"/api/meetings/{Id}/agenda/{SelectedActiveItemId}/notes",
                new AgendaItemNotesRequest {
                    MeetingId = Id,
                    ItemId = SelectedActiveItemId.Value,
                    Notes = ActiveItemNotes
                });
            _notesDirty = false;
            NotesSavedAt = DateTime.Now;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        NotesSaving = false;
        StateHasChanged();
    }

    private void OnResolutionInput(ChangeEventArgs e) {
        ActiveItemResolution = e.Value?.ToString() ?? "";
    }

    private async Task SaveActiveItemResolution() {
        if (SelectedActiveItem == null || Meeting == null)
            return;
        var item = SelectedActiveItem;
        if (item.Resolution == ActiveItemResolution)
            return;
        try {
            await Http.PutAsJsonAsync($"/api/meetings/{Id}/agenda/{item.Id}",
                new AgendaItemUpdateRequest {
                    MeetingId = Id,
                    ItemId = item.Id,
                    Title = item.Title,
                    ItemType = item.ItemType,
                    MotionId = item.MotionId,
                    Notes = item.Notes,
                    Resolution = ActiveItemResolution
                });
            item.Resolution = ActiveItemResolution;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CastVote(Guid agendaItemId, int vote) {
        if (CurrentUserId == null)
            return;
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{agendaItemId}/vote",
                new AgendaItemVoteRequest {
                    MeetingId = Id,
                    ItemId = agendaItemId,
                    UserId = CurrentUserId.Value,
                    Vote = vote
                });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CloseVote(Guid agendaItemId) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{agendaItemId}/close-vote", new { });
            ToastService.Toast("Abstimmung beendet.", "success");
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ChangeStatus(MeetingStatus target) {
        if (Meeting == null)
            return;
        var confirm = target switch {
            MeetingStatus.Archived => "Diese Sitzung wirklich archivieren? Ein PDF-Snapshot wird erstellt.",
            MeetingStatus.Completed => "Sitzung wirklich abschließen?",
            _ => null
        };
        if (confirm != null && !await ConfirmDialog.ShowAsync(confirm))
            return;
        try {
            var resp = await Http.PutAsJsonAsync($"/api/meetings/{Id}/status", new MeetingStatusUpdateRequest {
                Id = Id,
                Status = target
            });
            if (resp.IsSuccessStatusCode) {
                ToastService.Toast($"Status geändert: {StatusToLabel(target)}.", "success");
                await LoadMeeting();
            } else {
                var body = await resp.Content.ReadAsStringAsync();
                ToastService.Error(details: body);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task DeleteMeeting() {
        if (!await ConfirmDialog.ShowAsync("Diese Sitzung wirklich löschen?"))
            return;
        try {
            await Http.DeleteAsync($"/api/meetings/{Id}");
            ToastService.Toast("Sitzung gelöscht.", "success");
            Navigation.NavigateTo("/Administration/Meetings");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<(MeetingStatus Target, string Label, string Icon)> AllowedTransitions => Meeting?.Status switch {
        MeetingStatus.Draft => [(MeetingStatus.Scheduled, "Planen", "bi-calendar-check")],
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

    private static string StatusToLabel(MeetingStatus s) => s switch {
        MeetingStatus.Draft => "Entwurf",
        MeetingStatus.Scheduled => "Geplant",
        MeetingStatus.InProgress => "Laufend",
        MeetingStatus.Completed => "Abgeschlossen",
        MeetingStatus.Archived => "Archiviert",
        _ => s.ToString()
    };

    private static string VisibilityToLabel(MeetingVisibility v) => v switch {
        MeetingVisibility.Public => "Öffentlich",
        MeetingVisibility.Private => "Privat",
        _ => v.ToString()
    };

    private static string GetStatusBadgeClass(MeetingStatus s) => s switch {
        MeetingStatus.Draft => "border-secondary text-secondary-emphasis",
        MeetingStatus.Scheduled => "border-primary text-primary-emphasis",
        MeetingStatus.InProgress => "border-warning text-warning-emphasis",
        MeetingStatus.Completed => "border-success text-success-emphasis",
        MeetingStatus.Archived => "border-secondary text-body-tertiary",
        _ => "border-secondary"
    };

    private static string GetVisibilityBadgeClass(MeetingVisibility v) => v switch {
        MeetingVisibility.Public => "border-info text-info-emphasis",
        MeetingVisibility.Private => "border-secondary text-secondary-emphasis",
        _ => "border-secondary"
    };
}
