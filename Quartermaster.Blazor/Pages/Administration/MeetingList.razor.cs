using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MeetingListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private string SelectedChapterIdString = "";
    private string StatusFilter = "";
    private string VisibilityFilter = "";
    private string DateFromFilter = "";
    private string DateToFilter = "";

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

    private async Task OnStatusFilterChanged(ChangeEventArgs e) {
        StatusFilter = e.Value?.ToString() ?? "";
        CurrentPage = 1;
        await Search();
    }

    private async Task OnVisibilityFilterChanged(ChangeEventArgs e) {
        VisibilityFilter = e.Value?.ToString() ?? "";
        CurrentPage = 1;
        await Search();
    }

    private async Task OnDateFromChanged(ChangeEventArgs e) {
        DateFromFilter = e.Value?.ToString() ?? "";
        CurrentPage = 1;
        await Search();
    }

    private async Task OnDateToChanged(ChangeEventArgs e) {
        DateToFilter = e.Value?.ToString() ?? "";
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

        try {
            var url = $"/api/meetings?page={CurrentPage}&pageSize={PageSize}";
            if (Guid.TryParse(SelectedChapterIdString, out var chapterId))
                url += $"&chapterId={chapterId}";
            if (!string.IsNullOrEmpty(StatusFilter))
                url += $"&status={StatusFilter}";
            if (!string.IsNullOrEmpty(VisibilityFilter))
                url += $"&visibility={VisibilityFilter}";
            if (!string.IsNullOrEmpty(DateFromFilter))
                url += $"&dateFrom={DateFromFilter}";
            if (!string.IsNullOrEmpty(DateToFilter))
                url += $"&dateTo={DateToFilter}";

            Response = await Http.GetFromJsonAsync<MeetingListResponse>(url);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }
}
