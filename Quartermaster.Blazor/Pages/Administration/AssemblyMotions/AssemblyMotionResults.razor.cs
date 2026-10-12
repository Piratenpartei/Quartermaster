using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components.AssemblyMotions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.AssemblyMotions;

public partial class AssemblyMotionResults {
    private const int PageSize = 100;

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private readonly Dictionary<Guid, AssemblyMotionResult> _saved = [];
    private readonly Dictionary<Guid, AssemblyMotionResult> _selected = [];
    private AssemblyMotionPortalDTO? Portal;
    private List<AssemblyMotionDashboardItemDTO> Items = [];
    private bool Loading = true;
    private bool Forbidden;
    private bool Busy;

    private bool EventReached => Portal != null && AssemblyMotionDisplay.IsEventReached(Portal.EventDate);

    private string LockedText => AssemblyMotionDisplay.ResultsLockedText(I18n, Portal?.EventDate);

    private int ChangedCount => _selected.Count(s => _saved[s.Key] != s.Value);

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        Forbidden = false;
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            Portal = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            if (Portal != null)
                await LoadMotionsAsync(Portal.Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private async Task LoadMotionsAsync(Guid portalId) {
        var loaded = new List<AssemblyMotionDashboardItemDTO>();
        var page = 1;
        int totalCount;
        do {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/{portalId}/dashboard?Page={page}&PageSize={PageSize}");
            if (response.StatusCode == HttpStatusCode.Forbidden) {
                Forbidden = true;
                return;
            }
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();
            if (result == null)
                return;

            loaded.AddRange(result.Items);
            totalCount = result.TotalCount;
            page++;
        } while (loaded.Count < totalCount && page <= totalCount / PageSize + 1);

        Items = loaded
            .Where(i => !string.IsNullOrEmpty(i.DisplayNumber))
            .ToList();
        _saved.Clear();
        _selected.Clear();
        foreach (var item in Items) {
            _saved[item.Id] = item.Result;
            _selected[item.Id] = item.Result;
        }
    }

    private string GetSelection(Guid motionId) => ((int)_selected[motionId]).ToString();

    private void SetSelection(Guid motionId, string value) {
        if (int.TryParse(value, out var parsed))
            _selected[motionId] = (AssemblyMotionResult)parsed;
    }

    private string ResultOptionLabel(AssemblyMotionResult result) => AssemblyMotionDisplay.ResultOptionLabel(I18n, result);

    private async Task Save() {
        var changed = _selected
            .Where(s => _saved[s.Key] != s.Value)
            .ToList();
        if (Portal == null || !EventReached)
            return;
        if (changed.Count == 0) {
            ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ResultsNoChanges, "info");
            return;
        }

        var request = new AssemblyMotionBulkResultRequest {
            PortalId = Portal.Id,
            Items = changed
                .Select(s => new AssemblyMotionResultItem {
                    MotionId = s.Key,
                    Result = s.Value
                })
                .ToList()
        };

        Busy = true;
        try {
            var response = await Http.PutAsJsonAsync($"/api/assemblymotions/portals/{Portal.Id}/results", request);
            if (response.IsSuccessStatusCode) {
                foreach (var item in request.Items)
                    _saved[item.MotionId] = item.Result;
                ToastService.ToastKey(I18nKey.Ui.AssemblyMotion.ToastResultsSaved);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }
}
