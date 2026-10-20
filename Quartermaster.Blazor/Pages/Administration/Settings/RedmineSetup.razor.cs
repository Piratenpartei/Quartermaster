using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

/// <summary>The federal Redmine connection and chapters' own instances. Keys are write-only: an empty key field keeps the stored one.</summary>
public partial class RedmineSetup {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private bool Loading = true;
    private bool Busy;
    private Guid? FederalChapterId;
    private string FederalChapterName = "";
    private string FederalUrl = "";
    private string FederalKey = "";
    private bool FederalHasKey;
    private List<RedmineConnectionDTO> ChapterConnections = [];
    private readonly Dictionary<Guid, string> EditUrls = [];
    private readonly Dictionary<Guid, string> EditKeys = [];
    private readonly Dictionary<Guid, ConnectionCheckResultDTO> CheckResults = [];
    private string NewChapterId = "";
    private string NewUrl = "";
    private string NewKey = "";
    private ConfirmDialog RemoveDialog = default!;

    protected override async Task OnInitializedAsync() {
        try {
            await ReloadAsync();
        } finally {
            Loading = false;
        }
    }

    private async Task ReloadAsync() {
        try {
            var chapters = await Http.GetFromJsonAsync<List<ForwardingChapterSettingDTO>>("/api/forwarding/chapters") ?? [];
            var connections = await Http.GetFromJsonAsync<List<RedmineConnectionDTO>>("/api/forwarding/redmine-connections") ?? [];
            var federal = chapters.FirstOrDefault(c => c.IsFederal);
            FederalChapterId = federal?.ChapterId;
            FederalChapterName = federal?.ChapterName ?? "";

            var federalConnection = connections.FirstOrDefault(c => c.ChapterId == FederalChapterId);
            FederalUrl = federalConnection?.BaseUrl ?? "";
            FederalKey = "";
            FederalHasKey = federalConnection?.HasApiKey ?? false;

            ChapterConnections = connections.Where(c => c.ChapterId != FederalChapterId).ToList();
            EditUrls.Clear();
            EditKeys.Clear();
            foreach (var connection in ChapterConnections) {
                EditUrls[connection.ChapterId] = connection.BaseUrl;
                EditKeys[connection.ChapterId] = "";
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<Guid> ConfiguredChapterIds() {
        var ids = ChapterConnections.Select(c => c.ChapterId).ToList();
        if (FederalChapterId != null)
            ids.Add(FederalChapterId.Value);
        return ids;
    }

    private async Task SaveAsync(Guid chapterId, string url, string key) {
        Busy = true;
        try {
            using var response = await Http.PutAsJsonAsync($"/api/forwarding/redmine-connections/{chapterId}", new RedmineConnectionSaveRequest {
                ChapterId = chapterId,
                BaseUrl = url,
                ApiKey = string.IsNullOrWhiteSpace(key) ? null : key
            });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            CheckResults.Remove(chapterId);
            await ReloadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task AddAsync() {
        if (!Guid.TryParse(NewChapterId, out var chapterId))
            return;

        await SaveAsync(chapterId, NewUrl, NewKey);
        if (ChapterConnections.Any(c => c.ChapterId == chapterId)) {
            NewChapterId = "";
            NewUrl = "";
            NewKey = "";
        }
    }

    private async Task CheckAsync(Guid chapterId) {
        Busy = true;
        try {
            using var response = await Http.PostAsJsonAsync($"/api/forwarding/redmine-connections/{chapterId}/check", new { });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<ConnectionCheckResultDTO>();
            if (result != null)
                CheckResults[chapterId] = result;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task RemoveAsync(Guid chapterId) {
        if (!await RemoveDialog.ShowAsync(I18n[I18nKey.Ui.RedmineSetup.RemoveConfirm]))
            return;

        Busy = true;
        try {
            using var response = await Http.DeleteAsync($"/api/forwarding/redmine-connections/{chapterId}");
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            CheckResults.Remove(chapterId);
            await ReloadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }
}
