using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingLive : IDisposable {
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
    private List<AgendaTreeEntry> FlatItems = new();

    private Guid? SelectedActiveItemId;
    private string ActiveItemNotes = "";
    private bool NotesSaving;
    private DateTime? NotesSavedAt;
    private Timer? _notesDebounce;
    private bool _notesDirty;
    private string? _motionNotesTemplate;

    private Guid? CurrentUserId => AuthService.CurrentUser?.Id;

    private AgendaItemDTO? SelectedActiveItem =>
        SelectedActiveItemId == null
            ? null
            : Meeting?.AgendaItems.FirstOrDefault(a => a.Id == SelectedActiveItemId);

    protected override async Task OnInitializedAsync() {
        await LoadMeeting();
        await LoadMotionNotesTemplate();
    }

    public void Dispose() {
        _notesDebounce?.Dispose();
    }

    private async Task LoadMeeting() {
        Loading = true;
        try {
            Meeting = await Http.GetFromJsonAsync<MeetingDetailDTO>($"/api/meetings/{Id}");
            if (Meeting != null) {
                FlatItems = BuildFlatList(Meeting.AgendaItems);
                if (SelectedActiveItemId == null) {
                    var inProgress = Meeting.AgendaItems.FirstOrDefault(a => a.StartedAt != null && a.CompletedAt == null);
                    SelectedActiveItemId = inProgress?.Id ?? FlatItems.FirstOrDefault()?.Item.Id;
                    LoadActiveItemFields();
                }
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private async Task LoadMotionNotesTemplate() {
        try {
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>(
                "/api/options");
            var templateOption = options?.FirstOrDefault(o => o.Identifier == "meetings.motion_notes_template");
            if (templateOption != null)
                _motionNotesTemplate = templateOption.GlobalValue;
        } catch {
            // Non-critical
        }
    }

    private void LoadActiveItemFields() {
        var active = SelectedActiveItem;
        ActiveItemNotes = active?.Notes ?? "";
        NotesSavedAt = null;
        _notesDirty = false;

        if (active != null && active.ItemType == AgendaItemType.Motion &&
            string.IsNullOrEmpty(ActiveItemNotes) && !string.IsNullOrEmpty(_motionNotesTemplate)) {
            ActiveItemNotes = ApplyMotionNotesTemplate(_motionNotesTemplate, active);
            _notesDirty = true;
        }
    }

    private static string ApplyMotionNotesTemplate(string template, AgendaItemDTO item) {
        return template
            .Replace("{{ motion.Title }}", item.MotionTitle ?? "")
            .Replace("{{ motion.AuthorName }}", "")
            .Replace("{{ motion.Text }}", "");
    }

    private async Task OnSelectActiveItem(Guid itemId) {
        if (SelectedActiveItemId != null && SelectedActiveItemId != itemId) {
            await FlushNotes();
        }
        SelectedActiveItemId = itemId;
        LoadActiveItemFields();
        StateHasChanged();
    }

    private void OnNotesChanged(string value) {
        ActiveItemNotes = value;
        _notesDirty = true;
        _notesDebounce?.Dispose();
        _notesDebounce = new Timer(_ => InvokeAsync(FlushNotes), null, 3000, Timeout.Infinite);
    }

    private async Task StartAgendaItem(Guid itemId) {
        try {
            await FlushNotes();
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/start", new { });
            await LoadMeeting();
            SelectedActiveItemId = itemId;
            LoadActiveItemFields();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CompleteAgendaItem(Guid itemId) {
        try {
            await FlushNotes();
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/complete", new { });
            AdvanceToNextItem(itemId);
            await LoadMeeting();
            LoadActiveItemFields();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReopenAgendaItem(Guid itemId) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{itemId}/reopen", new { });
            await LoadMeeting();
            LoadActiveItemFields();
            ToastService.Toast("TOP neu geöffnet.", "success");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task FinishMeeting() {
        var confirmed = await ConfirmDialog.ShowAsync(
            "Möchten Sie die Sitzung abschließen? Offene Anträge werden automatisch aufgelöst.");
        if (!confirmed)
            return;
        try {
            await FlushNotes();
            await Http.PutAsJsonAsync($"/api/meetings/{Id}/status",
                new MeetingStatusUpdateRequest { Id = Id, Status = MeetingStatus.Completed });
            ToastService.Toast("Sitzung beendet.", "success");
            Navigation.NavigateTo($"/Administration/Meetings/{Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void AdvanceToNextItem(Guid completedId) {
        var idx = FlatItems.FindIndex(e => e.Item.Id == completedId);
        if (idx >= 0 && idx + 1 < FlatItems.Count)
            SelectedActiveItemId = FlatItems[idx + 1].Item.Id;
    }

    private async Task FlushNotes() {
        if (!_notesDirty || SelectedActiveItemId == null)
            return;
        _notesDebounce?.Dispose();
        NotesSaving = true;
        StateHasChanged();
        try {
            await Http.PutAsJsonAsync($"/api/meetings/{Id}/agenda/{SelectedActiveItemId}/notes",
                new AgendaItemNotesRequest {
                    MeetingId = Id,
                    ItemId = SelectedActiveItemId.Value,
                    Notes = ActiveItemNotes
                });
            // Update in-memory DTO so switching back doesn't revert to stale data.
            var item = Meeting?.AgendaItems.FirstOrDefault(a => a.Id == SelectedActiveItemId);
            if (item != null)
                item.Notes = ActiveItemNotes;
            _notesDirty = false;
            NotesSavedAt = DateTime.Now;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        NotesSaving = false;
        StateHasChanged();
    }

    private async Task CastVoteFor(Guid agendaItemId, Guid targetUserId, int vote) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{agendaItemId}/vote",
                new AgendaItemVoteRequest {
                    MeetingId = Id,
                    ItemId = agendaItemId,
                    UserId = targetUserId,
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

    private async Task TogglePresence(Guid agendaItemId, Guid userId, bool present) {
        try {
            await Http.PostAsJsonAsync($"/api/meetings/{Id}/agenda/{agendaItemId}/presence",
                new { MeetingId = Id, ItemId = agendaItemId, UserId = userId, Present = present });
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private string GetProgressRowClass(AgendaItemDTO item, bool isActive) {
        if (item.CompletedAt != null)
            return "bg-success-subtle";
        if (isActive)
            return "bg-primary-subtle";
        return "";
    }

    private static string VoteLabel(int? vote) => vote switch {
        0 => "Ja",
        1 => "Nein",
        2 => "Enthaltung",
        _ => "\u2014"
    };

    private static string GetMotionStatusLabel(int status) => status switch {
        0 => "Ausstehend",
        1 => "Angenommen",
        2 => "Abgelehnt",
        _ => "Unbekannt"
    };

    private static string GetMotionStatusBadgeClass(int status) => status switch {
        0 => "border-secondary text-secondary-emphasis",
        1 => "border-success text-success-emphasis",
        2 => "border-danger text-danger-emphasis",
        _ => "border-secondary"
    };

    private static List<AgendaTreeEntry> BuildFlatList(List<AgendaItemDTO> items) {
        var result = new List<AgendaTreeEntry>();
        var byParent = items
            .GroupBy(i => i.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(x => x.SortOrder).ToList());
        AppendLevel(result, byParent, null, 0, "");
        return result;
    }

    private static void AppendLevel(
        List<AgendaTreeEntry> result,
        Dictionary<Guid, List<AgendaItemDTO>> byParent,
        Guid? parentId,
        int depth,
        string prefix) {
        var key = parentId ?? Guid.Empty;
        if (!byParent.TryGetValue(key, out var children))
            return;
        var idx = 1;
        foreach (var child in children) {
            var number = string.IsNullOrEmpty(prefix) ? idx.ToString() : $"{prefix}.{idx}";
            result.Add(new AgendaTreeEntry { Item = child, Depth = depth, Number = number });
            AppendLevel(result, byParent, child.Id, depth + 1, number);
            idx++;
        }
    }
}
