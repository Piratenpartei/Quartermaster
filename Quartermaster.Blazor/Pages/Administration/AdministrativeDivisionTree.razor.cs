using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class AdministrativeDivisionTree {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<TreeNodeModel>? RootNodes;

    protected override async Task OnInitializedAsync() {
        try {
            var roots = await Http.GetFromJsonAsync<List<AdministrativeDivisionDTO>>(
                "/api/administrativedivisions/roots");

            RootNodes = roots?.Select(c => new TreeNodeModel(c)).ToList() ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}

public class TreeNodeModel {
    public AdministrativeDivisionDTO Division { get; }
    public List<TreeNodeModel>? Children { get; set; }
    public bool Expanded { get; set; }
    public bool Loading { get; set; }
    public bool IsLeaf { get; set; }

    public TreeNodeModel(AdministrativeDivisionDTO division) {
        Division = division;
    }
}
