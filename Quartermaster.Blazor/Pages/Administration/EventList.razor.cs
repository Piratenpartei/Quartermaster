using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventList {
    [Inject]
    public required HttpClient Http { get; set; }

    private EventSearchResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private string SelectedChapterIdString = "";
    private bool IncludeArchived;

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
        await Search();
    }

    private async Task OnChapterFilterChanged(string value) {
        SelectedChapterIdString = value;
        CurrentPage = 1;
        await Search();
    }

    private async Task OnIncludeArchivedChanged(ChangeEventArgs e) {
        IncludeArchived = (bool)(e.Value ?? false);
        CurrentPage = 1;
        await Search();
    }

    private async Task GoToPage(int selectedPage) {
        CurrentPage = selectedPage;
        await Search();
    }

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        var url = $"/api/events?page={CurrentPage}&pageSize={PageSize}&includeArchived={IncludeArchived}";
        if (Guid.TryParse(SelectedChapterIdString, out var chapterId))
            url += $"&chapterId={chapterId}";

        Response = await Http.GetFromJsonAsync<EventSearchResponse>(url);

        Loading = false;
        StateHasChanged();
    }
}
