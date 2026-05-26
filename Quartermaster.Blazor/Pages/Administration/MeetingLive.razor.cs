using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Api;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingLive : IAsyncDisposable {
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
    public required MeetingHubClient HubClient { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ConfirmDialog ConfirmDialog = default!;
    private MeetingDetailDTO? Meeting;
    private bool Loading = true;
    private List<AgendaTreeEntry> FlatItems = new();

    private Guid? SelectedActiveItemId;
    private string ActiveItemNotes = "";
    private string? _motionNotesTemplate;

    private Guid? CurrentUserId => AuthService.CurrentUser?.Id;

    private AgendaItemDTO? SelectedActiveItem =>
        SelectedActiveItemId == null
            ? null
            : Meeting?.AgendaItems.FirstOrDefault(a => a.Id == SelectedActiveItemId);

    protected override async Task OnInitializedAsync() {
        await LoadMeeting();
        await LoadMotionNotesTemplate();
        await ConnectHub();
    }

    private async Task ConnectHub() {
        HubClient.AgendaItemChanged += OnHubAgendaItemChanged;
        HubClient.MeetingStatusChanged += OnHubMeetingStatusChanged;
        HubClient.PresenceChanged += OnHubPresenceChanged;
        try {
            await HubClient.JoinMeetingAsync(Id);
        } catch (Exception ex) {
            // Non-fatal — live updates disabled, REST still works.
            Console.Error.WriteLine($"MeetingLive.ConnectHub: hub join failed; live updates off. {ex}");
        }
    }

    private void OnHubAgendaItemChanged(AgendaItemChangedMessage msg) {
        if (msg.MeetingId != Id)
            return;
        InvokeAsync(async () => {
            await LoadMeeting();
            StateHasChanged();
        });
    }

    private void OnHubMeetingStatusChanged(MeetingStatusChangedMessage msg) {
        if (msg.MeetingId != Id)
            return;
        InvokeAsync(async () => {
            await LoadMeeting();
            StateHasChanged();
        });
    }

    private void OnHubPresenceChanged(PresenceChangedMessage msg) {
        if (msg.MeetingId != Id)
            return;
        InvokeAsync(async () => {
            await LoadMeeting();
            StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync() {
        HubClient.AgendaItemChanged -= OnHubAgendaItemChanged;
        HubClient.MeetingStatusChanged -= OnHubMeetingStatusChanged;
        HubClient.PresenceChanged -= OnHubPresenceChanged;
        try {
            await HubClient.LeaveMeetingAsync(Id);
        } catch (Exception ex) {
            Console.Error.WriteLine($"MeetingLive.DisposeAsync: hub leave failed (best-effort). {ex}");
        }
    }

    private async Task LoadMeeting() {
        Loading = true;
        try {
            Meeting = await MeetingsApi.GetAsync(Id);
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
        } catch (Exception ex) {
            // Non-critical — motion-notes template feature degrades silently.
            Console.Error.WriteLine($"MeetingLive.LoadMotionNotesTemplate: option fetch failed. {ex}");
        }
    }

    private void LoadActiveItemFields() {
        var active = SelectedActiveItem;
        ActiveItemNotes = active?.Notes ?? "";

        if (active != null && active.ItemType == AgendaItemType.Motion &&
            string.IsNullOrEmpty(ActiveItemNotes) && !string.IsNullOrEmpty(_motionNotesTemplate)) {
            ActiveItemNotes = ApplyMotionNotesTemplate(_motionNotesTemplate, active);
        }
    }

    private static string ApplyMotionNotesTemplate(string template, AgendaItemDTO item) {
        return template
            .Replace("{{ motion.Title }}", item.MotionTitle ?? "")
            .Replace("{{ motion.AuthorName }}", "")
            .Replace("{{ motion.Text }}", "");
    }

    private void OnSelectActiveItem(Guid itemId) {
        SelectedActiveItemId = itemId;
        LoadActiveItemFields();
        StateHasChanged();
    }

    private void OnNotesChanged(string value) {
        // Notes persistence is handled by the collaborative editor itself
        // (Yjs snapshot save timer on the hub). We only mirror the text
        // locally so the preview pane and the active-item DTO stay in sync.
        ActiveItemNotes = value;
    }

    private async Task StartAgendaItem(Guid itemId) {
        try {
            await MeetingsApi.StartAgendaItemAsync(Id, itemId);
            await LoadMeeting();
            SelectedActiveItemId = itemId;
            LoadActiveItemFields();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CompleteAgendaItem(Guid itemId) {
        try {
            await MeetingsApi.CompleteAgendaItemAsync(Id, itemId);
            AdvanceToNextItem(itemId);
            await LoadMeeting();
            LoadActiveItemFields();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReopenAgendaItem(Guid itemId) {
        try {
            await MeetingsApi.ReopenAgendaItemAsync(Id, itemId);
            await LoadMeeting();
            LoadActiveItemFields();
            ToastService.ToastKey(I18nKey.Ui.Toast.TopReopened);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task FinishMeeting() {
        var confirmed = await ConfirmDialog.ShowAsync(
            ToastService.Translate(I18nKey.Ui.Confirm.MeetingFinish));
        if (!confirmed)
            return;
        try {
            await MeetingsApi.UpdateStatusAsync(
                new MeetingStatusUpdateRequest { Id = Id, Status = MeetingStatus.Completed });
            ToastService.ToastKey(I18nKey.Ui.Toast.MeetingEnded);
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

    private async Task CastVoteFor(Guid agendaItemId, Guid targetUserId, VoteType vote) {
        try {
            await MeetingsApi.VoteAgendaItemAsync(new AgendaItemVoteRequest {
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
            await MeetingsApi.CloseVoteAgendaItemAsync(Id, agendaItemId);
            ToastService.ToastKey(I18nKey.Ui.Toast.VoteEnded);
            await LoadMeeting();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task TogglePresence(Guid agendaItemId, Guid userId, bool present) {
        try {
            await MeetingsApi.SetPresenceAsync(new AgendaItemPresenceRequest {
                MeetingId = Id,
                ItemId = agendaItemId,
                UserId = userId,
                Present = present
            });
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
