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

public partial class AssemblyMotionMine {
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

    private List<AssemblyMotionMineItemDTO> Items = [];
    private bool Loading = true;
    private bool Forbidden;

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        Forbidden = false;
        try {
            await AuthService.WaitForInitializationAsync();
            if (!AuthService.IsAuthenticated)
                return;

            var portalResponse = await Http.GetAsync($"/api/assemblymotions/portals/by-event/{EventId}");
            var portal = portalResponse.IsSuccessStatusCode ? await portalResponse.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>() : null;
            if (portal != null) {
                var response = await Http.GetAsync($"/api/assemblymotions/portals/{portal.Id}/mine");
                if (response.IsSuccessStatusCode) {
                    Items = await response.Content.ReadFromJsonAsync<List<AssemblyMotionMineItemDTO>>() ?? [];
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
}
