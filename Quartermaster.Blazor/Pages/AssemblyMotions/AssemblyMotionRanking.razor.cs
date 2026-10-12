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

namespace Quartermaster.Blazor.Pages.AssemblyMotions;

public partial class AssemblyMotionRanking {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private AssemblyMotionPortalDTO? Portal;
    private List<AssemblyMotionRankingItemDTO> Items = [];
    private bool Loading = true;
    private bool NotAvailable;
    private bool ByType;

    private string NotAvailableText {
        get {
            if (Portal == null || !Portal.PrioritizationEnabled || Portal.PrioritizationEndsAt == null)
                return I18n[I18nKey.Ui.AssemblyMotion.RankingNotEnabled];

            return I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.RankingNotAvailable, ("date", AssemblyMotionDisplay.Berlin(Portal.PrioritizationEndsAt.Value)))];
        }
    }

    private List<IGrouping<Guid, AssemblyMotionRankingItemDTO>> Groups => Items
        .GroupBy(i => i.TypeId)
        .OrderBy(g => g.First().TypeSortOrder)
        .ToList();

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        NotAvailable = false;
        Items = [];
        try {
            var response = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            Portal = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            if (Portal != null)
                await LoadRankingAsync(Portal.Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private async Task LoadRankingAsync(Guid portalId) {
        var response = await Http.GetAsync($"/api/assemblymotions/portals/{portalId}/ranking");
        if (response.StatusCode == HttpStatusCode.Conflict) {
            NotAvailable = true;
        } else if (response.IsSuccessStatusCode) {
            var ranking = await response.Content.ReadFromJsonAsync<AssemblyMotionRankingResponse>();
            Items = ranking?.Items ?? [];
        } else {
            await ToastService.ErrorAsync(response);
        }
    }

    private void SwitchView(bool byType) {
        ByType = byType;
    }
}
