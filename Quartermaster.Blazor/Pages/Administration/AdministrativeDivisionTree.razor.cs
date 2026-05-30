using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class AdministrativeDivisionTree {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

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

    private string DepthLabel(int depth) => depth switch {
        1 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthWorld],
        3 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthCountry],
        4 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthFederalState],
        5 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthGovernmentRegion],
        6 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthDistrict],
        7 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthMunicipality],
        8 => I18n[I18nKey.Ui.AdminDivisionSearch.DepthLocality],
        _ => depth.ToString()
    };
}
