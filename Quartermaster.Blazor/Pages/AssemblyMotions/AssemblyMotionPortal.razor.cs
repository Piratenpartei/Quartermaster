using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
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
    private List<AssemblyMotionPublicListItemDTO> Items = [];
    private bool Loading = true;
    private bool ListLoading;
    private int CurrentPage = 1;
    private int TotalPages;
    private string FilterTypeId = "";
    private string FilterStatus = "";
    private string Search = "";

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            Portal = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            if (Portal != null) {
                Context = await Http.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{Portal.Id}/context");
                await LoadMotionsAsync();
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
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

    private List<IGrouping<Guid, AssemblyMotionPublicListItemDTO>> GetGroups()
        => Items
            .GroupBy(i => i.TypeId)
            .OrderBy(g => Portal!.Types.FindIndex(t => t.Id == g.Key))
            .ToList();

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
