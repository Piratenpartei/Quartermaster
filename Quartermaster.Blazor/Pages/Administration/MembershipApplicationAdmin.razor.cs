using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MembershipApplicationAdmin {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private MembershipApplicationListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private string SelectedChapterIdString = "";
    private int? SelectedStatus = 0;

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

    private async Task OnStatusChanged(ChangeEventArgs e) {
        SelectedStatus = int.TryParse(e.Value?.ToString(), out var s) ? s : null;
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
            var url = $"/api/admin/membershipapplications?page={CurrentPage}&pageSize={PageSize}";
            if (Guid.TryParse(SelectedChapterIdString, out var chapterId))
                url += $"&chapterId={chapterId}";
            if (SelectedStatus.HasValue)
                url += $"&status={SelectedStatus.Value}";

            Response = await Http.GetFromJsonAsync<MembershipApplicationListResponse>(url);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }
}
