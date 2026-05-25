using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AdministrativeDivisions;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class TreeNode {
    [Parameter]
    public required TreeNodeModel Node { get; set; }

    [Parameter]
    public required HttpClient Http { get; set; }

    private async Task Toggle() {
        if (Node.Loading)
            return;

        if (!Node.Expanded && Node.Children == null) {
            Node.Loading = true;
            StateHasChanged();

            var children = await Http.GetFromJsonAsync<List<AdministrativeDivisionDTO>>(
                $"/api/administrativedivisions/{Node.Division.Id}/children");

            Node.Children = children?.Select(c => new TreeNodeModel(c)).ToList() ?? [];
            Node.Loading = false;
            Node.IsLeaf = Node.Children.Count == 0;
        }

        if (!Node.IsLeaf)
            Node.Expanded = !Node.Expanded;
        StateHasChanged();
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
