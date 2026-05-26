using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class AdministrativeDivisionTree {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<LazyTreeNodeModel<AdministrativeDivisionDTO>>? RootNodes;

    protected override async Task OnInitializedAsync() {
        try {
            var roots = await Http.GetFromJsonAsync<List<AdministrativeDivisionDTO>>(
                "/api/administrativedivisions/roots");
            RootNodes = roots?.Select(c => new LazyTreeNodeModel<AdministrativeDivisionDTO>(c)).ToList() ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task<List<AdministrativeDivisionDTO>> LoadDivisionChildren(AdministrativeDivisionDTO division) {
        var children = await Http.GetFromJsonAsync<List<AdministrativeDivisionDTO>>(
            $"/api/administrativedivisions/{division.Id}/children");
        return children ?? new List<AdministrativeDivisionDTO>();
    }

    private static string DepthLabel(int depth) => depth switch {
        1 => "Welt",
        3 => "Land",
        4 => "Bundesland",
        5 => "Regierungsbezirk",
        6 => "Kreis",
        7 => "Gemeinde",
        8 => "Ort",
        _ => depth.ToString()
    };
}
