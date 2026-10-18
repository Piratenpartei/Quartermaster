using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyMotions;

public partial class AssemblyMotionCollecting {
    private const int PageSize = 25;

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private AssemblyMotionPortalDTO? Portal;
    private List<AssemblyMotionCollectingItemDTO> Items = [];
    private bool Loading = true;
    private bool Forbidden;
    private int CurrentPage = 1;
    private int TotalPages;

    protected override async Task OnParametersSetAsync() {
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Loading = true;
        Forbidden = false;
        try {
            await AuthService.WaitForInitializationAsync();
            if (!AuthService.IsAuthenticated)
                return;

            if (Portal == null) {
                var portalResponse = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
                Portal = portalResponse.IsSuccessStatusCode ? await portalResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            }

            if (Portal != null) {
                var response = await Http.GetAsync($"/api/assemblymotions/portals/{Portal.Id}/collecting?Page={CurrentPage}&PageSize={PageSize}");
                if (response.IsSuccessStatusCode) {
                    var result = await response.Content.ReadFromJsonAsync<AssemblyMotionCollectingListResponse>();
                    Items = result?.Items ?? [];
                    TotalPages = result == null ? 0 : (int)Math.Ceiling(result.TotalCount / (double)PageSize);
                } else if (response.StatusCode == HttpStatusCode.Forbidden) {
                    Forbidden = true;
                } else {
                    await ToastService.ErrorAsync(response);
                }
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private async Task OnPageChanged(int page) {
        CurrentPage = page;
        await LoadAsync();
    }
}
