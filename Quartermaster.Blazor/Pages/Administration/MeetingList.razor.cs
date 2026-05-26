using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Api;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingList {
    [Inject]
    public required MeetingsApi MeetingsApi { get; set; }
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
            var query = $"page={CurrentPage}&pageSize={PageSize}";
            if (Guid.TryParse(SelectedChapterIdString, out var chapterId))
                query += $"&chapterId={chapterId}";
            if (!string.IsNullOrEmpty(StatusFilter))
                query += $"&status={StatusFilter}";
            if (!string.IsNullOrEmpty(VisibilityFilter))
                query += $"&visibility={VisibilityFilter}";
            if (!string.IsNullOrEmpty(DateFromFilter))
                query += $"&dateFrom={DateFromFilter}";
            if (!string.IsNullOrEmpty(DateToFilter))
                query += $"&dateTo={DateToFilter}";

            Response = await MeetingsApi.ListAsync(query);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }
}
