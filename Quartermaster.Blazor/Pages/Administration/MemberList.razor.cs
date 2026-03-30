using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Members;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MemberList {
    [Inject]
    public required HttpClient Http { get; set; }

    private List<ChapterDTO>? Chapters;
    private MemberSearchResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private string? SearchQuery;
    private Guid? SelectedChapterId;

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
        await Search();
    }

    private async Task OnChapterChanged(ChangeEventArgs e) {
        SelectedChapterId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        CurrentPage = 1;
        await Search();
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter") {
            CurrentPage = 1;
            await Search();
        }
    }

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var url = $"/api/members?page={CurrentPage}&pageSize={PageSize}";
        if (!string.IsNullOrWhiteSpace(SearchQuery))
            url += $"&query={Uri.EscapeDataString(SearchQuery)}";
        if (SelectedChapterId.HasValue)
            url += $"&chapterId={SelectedChapterId.Value}";

        Response = await Http.GetFromJsonAsync<MemberSearchResponse>(url);

        Loading = false;
        StateHasChanged();
    }
}
