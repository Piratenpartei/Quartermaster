using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class NewsPosts {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private readonly HashSet<string> _selectedChannels = [];

    private string view = "suggestions";
    private string statusFilter = "";
    private bool loading = true;
    private bool busy;
    private List<NewsItemDTO> items = [];
    private List<PostPublicationDTO> publications = [];
    private List<PostizChannelDTO>? channels;
    private Guid? editingItemId;
    private Guid? editingCandidateId;
    private string editText = "";
    private bool scheduled;
    private ConfirmDialog? confirmDialog;

    /// <summary>The datetime-local input yields a wall-clock time tagged DateTimeKind.Unspecified.
    /// DateTimeOffset(DateTime) in PublishAsync treats Unspecified the same as Local (both resolve
    /// against TimeZoneInfo.Local, which WASM sets from the browser), so forcing Local here does not
    /// change that conversion; it just documents the intent for any future reader of this value.</summary>
    private DateTime scheduledLocal {
        get;
        set => field = DateTime.SpecifyKind(value, DateTimeKind.Local);
    } = DateTime.Now.AddHours(1);

    private bool CanPublish => !busy && PostText.IsValid(editText) && _selectedChannels.Count > 0;

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
        await LoadChannelsAsync();
    }

    private async Task SwitchViewAsync(string newView) {
        view = newView;
        statusFilter = "";
        CloseEditor();
        await LoadAsync();
    }

    private async Task OnStatusFilterChanged(ChangeEventArgs e) {
        statusFilter = e.Value?.ToString() ?? "";
        await LoadAsync();
    }

    private async Task LoadAsync() {
        loading = true;
        try {
            if (view == "posted") {
                publications = await Http.GetFromJsonAsync<List<PostPublicationDTO>>("/api/newsposts/publications") ?? [];
            } else {
                var url = $"/api/newsposts/items?view={view}";
                if (statusFilter.Length > 0)
                    url += "&status=" + statusFilter;
                items = await Http.GetFromJsonAsync<List<NewsItemDTO>>(url) ?? [];
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        loading = false;
    }

    private async Task LoadChannelsAsync() {
        try {
            channels = await Http.GetFromJsonAsync<List<PostizChannelDTO>>("/api/newsposts/channels");
        } catch (HttpRequestException) {
            channels = null;
        }
    }

    private async Task PostActionAsync(NewsItemDTO item, string action) {
        busy = true;
        try {
            var response = await Http.PostAsync($"/api/newsposts/items/{item.Id}/{action}", null);
            if (!response.IsSuccessStatusCode)
                await ToastService.ErrorAsync(response);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        busy = false;
        await LoadAsync();
    }

    private void OpenEditor(NewsItemDTO item, PostCandidateDTO candidate) {
        editingItemId = item.Id;
        editingCandidateId = candidate.Id;
        editText = candidate.Text;
        scheduled = false;
        _selectedChannels.Clear();
    }

    private async Task RetryPublicationAsync(PostPublicationDTO publication) {
        view = "all";
        editingItemId = publication.NewsItemId;
        editingCandidateId = null;
        editText = publication.FinalText;
        _selectedChannels.Clear();
        foreach (var id in publication.ChannelIds)
            _selectedChannels.Add(id);
        scheduled = publication.ScheduledFor != null && publication.ScheduledFor > DateTimeOffset.Now;
        if (scheduled)
            scheduledLocal = publication.ScheduledFor!.Value.LocalDateTime;
        await LoadAsync();
    }

    private void CloseEditor() {
        editingItemId = null;
        editingCandidateId = null;
        editText = "";
    }

    private void ToggleChannel(string id, bool isChecked) {
        if (isChecked) {
            _selectedChannels.Add(id);
        } else {
            _selectedChannels.Remove(id);
        }
    }

    private async Task PublishAsync() {
        if (editingItemId == null || confirmDialog == null)
            return;
        if (!await confirmDialog.ShowAsync())
            return;

        busy = true;
        var request = new PublishRequest {
            CandidateId = editingCandidateId,
            Text = editText.Trim(),
            ChannelIds = _selectedChannels.ToList(),
            ScheduledFor = scheduled ? new DateTimeOffset(scheduledLocal) : null
        };
        try {
            var response = await Http.PostAsJsonAsync($"/api/newsposts/items/{editingItemId}/publish", request);
            if (response.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.NewsPosts.Published);
                CloseEditor();
            } else {
                await ToastService.ErrorAsync(response);
                await LoadChannelsAsync();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        busy = false;
        await LoadAsync();
    }

    private static bool CanGenerate(NewsItemDTO item) {
        if (item.Status == NewsItemStatusDTO.Posted)
            return false;
        if (item.Status == NewsItemStatusDTO.Posting)
            return false;
        return item.Status != NewsItemStatusDTO.Failed;
    }

    private static bool CanDismiss(NewsItemDTO item) {
        if (item.Status == NewsItemStatusDTO.Posted)
            return false;
        if (item.Status == NewsItemStatusDTO.Posting)
            return false;
        return item.Status != NewsItemStatusDTO.Dismissed;
    }

    private string StatusLabel(NewsItemStatusDTO status) => I18n[$"ui.news_posts.status.{status.ToString().ToLowerInvariant()}"];

    private string ChannelName(string id) => channels?.FirstOrDefault(c => c.Id == id)?.Name ?? id;
}
