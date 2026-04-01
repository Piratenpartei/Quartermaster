using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterTree {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<ChapterTreeNodeModel>? RootNodes;

    protected override async Task OnInitializedAsync() {
        try {
            var roots = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters/roots");
            RootNodes = roots?.Select(c => new ChapterTreeNodeModel(c)).ToList() ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}

public class ChapterTreeNodeModel {
    public ChapterDTO Chapter { get; }
    public List<ChapterTreeNodeModel>? Children { get; set; }
    public bool Expanded { get; set; }
    public bool Loading { get; set; }
    public bool IsLeaf { get; set; }

    public ChapterTreeNodeModel(ChapterDTO chapter) {
        Chapter = chapter;
    }
}
