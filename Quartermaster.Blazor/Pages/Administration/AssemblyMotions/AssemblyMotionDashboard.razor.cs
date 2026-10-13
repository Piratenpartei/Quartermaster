using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.AssemblyMotions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.AssemblyMotions;

public partial class AssemblyMotionDashboard {
    private const int PageSize = 25;

    private static readonly AssemblyMotionStatus[] _filterStatuses = [
        AssemblyMotionStatus.CollectingCoSubmitters,
        AssemblyMotionStatus.Submitted,
        AssemblyMotionStatus.Admitted,
        AssemblyMotionStatus.FormallyInsufficient,
        AssemblyMotionStatus.Withdrawn,
        AssemblyMotionStatus.Lapsed
    ];

    private static readonly WikiSyncState[] _wikiStates = [
        WikiSyncState.NotMirrored,
        WikiSyncState.Pending,
        WikiSyncState.Synced,
        WikiSyncState.Failed,
        WikiSyncState.ProtectionFailed
    ];

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private AssemblyMotionPortalDTO? Portal;
    private List<AssemblyMotionDashboardItemDTO> Items = [];
    private bool Loading = true;
    private ConfirmDialog ResyncAllDialog = default!;
    private OpenSlidesPushDialog PushDialog = default!;
    private OpenSlidesPushResultDTO? PushResult;
    private bool PushRunning;
    private bool OpenSlidesMeetingConfigured;
    private bool ListLoading;
    private bool Busy;
    private int CurrentPage = 1;
    private int TotalPages;
    private int FailedWikiSyncCount;
    private bool WikiMirrorEnabled;
    private bool FilterWikiFailed;
    private string FilterTypeId = "";
    private string FilterStatus = "";
    private string FilterInquiry = "";
    private string FilterWiki = "";
    private string Search = "";

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            Portal = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            if (Portal != null)
                await LoadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private async Task LoadAsync() {
        if (Portal == null)
            return;

        ListLoading = true;
        try {
            var query = $"?Page={CurrentPage}&PageSize={PageSize}";
            if (FilterTypeId != "")
                query += $"&TypeId={FilterTypeId}";
            if (FilterStatus != "")
                query += $"&Status={FilterStatus}";
            if (FilterInquiry != "")
                query += $"&HasOpenInquiry={FilterInquiry}";
            if (FilterWiki != "")
                query += $"&WikiSyncState={FilterWiki}";
            if (FilterWikiFailed)
                query += "&WikiFailed=true";
            if (!string.IsNullOrWhiteSpace(Search))
                query += $"&Search={Uri.EscapeDataString(Search.Trim())}";

            var response = await Http.GetAsync($"/api/assemblymotions/portals/{Portal.Id}/dashboard{query}");
            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();
                Items = result?.Items ?? [];
                FailedWikiSyncCount = result?.FailedWikiSyncCount ?? 0;
                WikiMirrorEnabled = result?.WikiMirrorEnabled ?? false;
                OpenSlidesMeetingConfigured = result?.OpenSlidesMeetingConfigured ?? false;
                TotalPages = result == null ? 0 : (int)Math.Ceiling(result.TotalCount / (double)PageSize);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        ListLoading = false;
    }

    private async Task OnTypeFilterChanged(string value) {
        FilterTypeId = value;
        await ReloadFromStartAsync();
    }

    private async Task OnStatusFilterChanged(string value) {
        FilterStatus = value;
        await ReloadFromStartAsync();
    }

    private async Task OnInquiryFilterChanged(string value) {
        FilterInquiry = value;
        await ReloadFromStartAsync();
    }

    private async Task OnWikiFilterChanged(string value) {
        FilterWiki = value;
        FilterWikiFailed = false;
        await ReloadFromStartAsync();
    }

    private void OnSearchInput(ChangeEventArgs e) {
        Search = e.Value?.ToString() ?? "";
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter")
            await ApplySearch();
    }

    private async Task ApplySearch() {
        await ReloadFromStartAsync();
    }

    private async Task OnPageChanged(int page) {
        CurrentPage = page;
        await LoadAsync();
    }

    private async Task ShowFailedWikiSyncsAsync() {
        FilterWiki = "";
        FilterWikiFailed = true;
        await ReloadFromStartAsync();
    }

    private async Task ResyncAllAsync() {
        if (Portal == null)
            return;
        if (Busy || PushRunning)
            return;
        if (!await ResyncAllDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.WikiResyncAllMessage]))
            return;

        Busy = true;
        StateHasChanged();
        try {
            var response = await Http.PostAsJsonAsync($"/api/assemblymotions/portals/{Portal.Id}/wiki/resync", new { });
            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<AssemblyMotionWikiResyncAllDTO>();
                ToastService.Toast(I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.ToastWikiResyncAll, ("count", (result?.Queued ?? 0).ToString()))], "success");
                await LoadAsync();
            } else {
                await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } catch (TaskCanceledException ex) {
            ToastService.Error(ex);
        } catch (JsonException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task PushToOpenSlidesAsync() {
        if (Portal == null || PushRunning || Busy)
            return;

        var includeSubmitted = await PushDialog.ShowAsync();
        if (includeSubmitted == null)
            return;

        PushRunning = true;
        StateHasChanged();
        try {
            var response = await Http.PostAsJsonAsync($"/api/assemblymotions/portals/{Portal.Id}/openslides/push", new OpenSlidesPushRequest {
                PortalId = Portal.Id,
                IncludeSubmitted = includeSubmitted.Value
            });
            if (response.IsSuccessStatusCode) {
                PushResult = await response.Content.ReadFromJsonAsync<OpenSlidesPushResultDTO>();
                await LoadAsync();
            } else {
                await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } catch (OperationCanceledException) {
            ToastService.ErrorKey(I18nKey.Ui.AssemblyMotion.OpenSlidesPushInterrupted);
            await LoadAsync();
        } catch (JsonException ex) {
            ToastService.Error(ex);
        } finally {
            PushRunning = false;
            StateHasChanged();
        }
    }

    private async Task ExportAsync() {
        if (Portal == null || Busy || PushRunning)
            return;

        Busy = true;
        StateHasChanged();
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/{Portal.Id}/export");
            if (response.IsSuccessStatusCode) {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = $"antraege-{BerlinTime.FromUtc(DateTime.UtcNow):yyyy-MM-dd}.json";
                await JS.InvokeVoidAsync("qmDownload.blob", bytes, fileName, "application/json");
            } else {
                await AssemblyMotionErrorHandler.ShowAsync(response, I18n, ToastService);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } catch (TaskCanceledException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task ReloadFromStartAsync() {
        CurrentPage = 1;
        await LoadAsync();
    }
}
