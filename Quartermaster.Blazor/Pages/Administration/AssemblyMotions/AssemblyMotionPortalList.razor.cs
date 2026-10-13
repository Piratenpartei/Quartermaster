using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.AssemblyMotions;

public partial class AssemblyMotionPortalList {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private List<AssemblyMotionManagedPortalDTO> Portals = [];
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            var response = await Http.GetAsync("/api/assemblymotions/portals/managed");
            if (response.IsSuccessStatusCode) {
                Portals = await response.Content.ReadFromJsonAsync<List<AssemblyMotionManagedPortalDTO>>() ?? [];
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }
}
