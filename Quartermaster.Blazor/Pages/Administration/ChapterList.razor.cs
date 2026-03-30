using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterList {
    [Inject]
    public required HttpClient Http { get; set; }

    private string SearchQuery { get; set; } = "";
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private bool Loading;
    private ChapterSearchResponse? Response;
    private CancellationTokenSource? _debounceTokenSource;

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        await Search();
    }

    private async Task OnSearchKeyUp() {
        _debounceTokenSource?.Cancel();
        _debounceTokenSource = new CancellationTokenSource();
        var token = _debounceTokenSource.Token;

        try {
            await Task.Delay(300, token);
            CurrentPage = 1;
            await Search();
        } catch (TaskCanceledException) { }
    }

    private async Task GoToPage(int page) {
        if (page < 1 || page > TotalPages)
            return;
        CurrentPage = page;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var query = string.IsNullOrWhiteSpace(SearchQuery) ? "" : SearchQuery;
        Response = await Http.GetFromJsonAsync<ChapterSearchResponse>(
            $"/api/chapters/search?query={Uri.EscapeDataString(query)}&page={CurrentPage}&pageSize={PageSize}");

        Loading = false;
        StateHasChanged();
    }
}
