using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components.AssemblyMotions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyMotions;

public partial class AssemblyMotionPortal {
    private const int PageSize = 25;

    private static readonly AssemblyMotionStatus[] _filterStatuses = [
        AssemblyMotionStatus.CollectingCoSubmitters,
        AssemblyMotionStatus.Submitted,
        AssemblyMotionStatus.Admitted,
        AssemblyMotionStatus.FormallyInsufficient,
        AssemblyMotionStatus.Withdrawn,
        AssemblyMotionStatus.Lapsed
    ];

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private AssemblyMotionPortalDTO? Portal;
    private AssemblyMotionPortalContextDTO? Context;
    private HashSet<Guid> UpvotedIds = [];
    private List<AssemblyMotionPublicListItemDTO> Items = [];
    private bool Loading = true;
    private bool ListLoading;
    private int CurrentPage = 1;
    private int TotalPages;
    private string FilterTypeId = "";
    private string FilterStatus = "";
    private string Search = "";

    private bool PrioritizationOpen => Portal != null && AssemblyMotionDisplay.IsPrioritizationOpen(Portal);

    private bool RankingAvailable => Portal != null && AssemblyMotionDisplay.IsRankingAvailable(Portal);

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            Portal = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            if (Portal != null) {
                Context = await Http.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{Portal.Id}/context");
                await LoadUpvotesSafeAsync();
                await LoadMotionsAsync();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private async Task LoadUpvotesSafeAsync() {
        try {
            await LoadUpvotesAsync();
        } catch (Exception ex) when (ex is HttpRequestException || ex is JsonException) {
            UpvotedIds = [];
        }
    }

    private async Task LoadUpvotesAsync() {
        UpvotedIds = [];
        if (Portal == null || Context == null || !PrioritizationOpen)
            return;
        if (!Context.IsSignedIn || Context.Problem == EligibilityProblem.NoLinkedMember)
            return;

        var response = await Http.GetAsync($"/api/assemblymotions/portals/{Portal.Id}/my-upvotes");
        if (!response.IsSuccessStatusCode)
            return;

        var ids = await response.Content.ReadFromJsonAsync<List<Guid>>();
        UpvotedIds = (ids ?? []).ToHashSet();
    }

    private async Task LoadMotionsAsync() {
        if (Portal == null)
            return;

        ListLoading = true;
        try {
            var query = $"?Page={CurrentPage}&PageSize={PageSize}";
            if (FilterTypeId != "")
                query += $"&TypeId={FilterTypeId}";
            if (FilterStatus != "")
                query += $"&Status={FilterStatus}";
            if (!string.IsNullOrWhiteSpace(Search))
                query += $"&Search={Uri.EscapeDataString(Search.Trim())}";

            var response = await Http.GetAsync($"/api/assemblymotions/portals/{Portal.Id}/motions{query}");
            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<AssemblyMotionPublicListResponse>();
                Items = result?.Items ?? [];
                TotalPages = result == null ? 0 : (int)Math.Ceiling(result.TotalCount / (double)PageSize);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        ListLoading = false;
    }

    private List<IGrouping<Guid, AssemblyMotionPublicListItemDTO>> GetGroups() {
        return Items
                .GroupBy(i => i.TypeId)
                .OrderBy(g => Portal!.Types.FindIndex(t => t.Id == g.Key))
                .ToList();
    }

    private async Task OnTypeFilterChanged(string value) {
        FilterTypeId = value;
        CurrentPage = 1;
        await LoadMotionsAsync();
    }

    private async Task OnStatusFilterChanged(string value) {
        FilterStatus = value;
        CurrentPage = 1;
        await LoadMotionsAsync();
    }

    private void OnSearchInput(ChangeEventArgs e) {
        Search = e.Value?.ToString() ?? "";
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter")
            await ApplySearch();
    }

    private async Task ApplySearch() {
        CurrentPage = 1;
        await LoadMotionsAsync();
    }

    private async Task OnPageChanged(int page) {
        CurrentPage = page;
        await LoadMotionsAsync();
    }
}
