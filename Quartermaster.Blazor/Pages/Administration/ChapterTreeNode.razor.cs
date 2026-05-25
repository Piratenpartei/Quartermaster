using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterTreeNode {
    [Parameter]
    public required ChapterTreeNodeModel Node { get; set; }

    [Parameter]
    public required HttpClient Http { get; set; }

    private async Task Toggle() {
        if (Node.Loading)
            return;

        if (!Node.Expanded && Node.Children == null) {
            Node.Loading = true;
            StateHasChanged();

            var children = await Http.GetFromJsonAsync<List<ChapterDTO>>(
                $"/api/chapters/{Node.Chapter.Id}/children");

            Node.Children = children?.Select(c => new ChapterTreeNodeModel(c)).ToList() ?? [];
            Node.Loading = false;
            Node.IsLeaf = Node.Children.Count == 0;
        }

        if (!Node.IsLeaf)
            Node.Expanded = !Node.Expanded;
        StateHasChanged();
    }
}
