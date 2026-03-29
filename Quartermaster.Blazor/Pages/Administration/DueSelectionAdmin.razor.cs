using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class DueSelectionAdmin {
    [Inject]
    public required HttpClient Http { get; set; }

    private DueSelectionListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private int? SelectedStatus = 0;

    private int TotalPages => Response == null ? 0
        : (int)Math.Ceiling((double)Response.TotalCount / PageSize);

    protected override async Task OnInitializedAsync() {
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

        var url = $"/api/admin/dueselections?page={CurrentPage}&pageSize={PageSize}";
        if (SelectedStatus.HasValue)
            url += $"&status={SelectedStatus.Value}";

        Response = await Http.GetFromJsonAsync<DueSelectionListResponse>(url);

        Loading = false;
        StateHasChanged();
    }
}
