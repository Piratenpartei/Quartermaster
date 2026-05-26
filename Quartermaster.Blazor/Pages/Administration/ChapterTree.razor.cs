using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterTree {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private List<LazyTreeNodeModel<ChapterDTO>>? RootNodes;

    protected override async Task OnInitializedAsync() {
        try {
            var roots = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters/roots");
            RootNodes = roots?.Select(c => new LazyTreeNodeModel<ChapterDTO>(c)).ToList() ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task<List<ChapterDTO>> LoadChapterChildren(ChapterDTO chapter) {
        var children = await Http.GetFromJsonAsync<List<ChapterDTO>>(
            $"/api/chapters/{chapter.Id}/children");
        return children ?? new List<ChapterDTO>();
    }
}
