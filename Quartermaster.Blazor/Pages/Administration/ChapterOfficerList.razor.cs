using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ChapterOfficerList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private ChapterOfficerSearchResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private string? SearchQuery;
    private string SelectedChapterIdString = "";

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

        try {
            var url = $"/api/chapterofficers?page={CurrentPage}&pageSize={PageSize}";
            if (!string.IsNullOrWhiteSpace(SearchQuery))
                url += $"&query={Uri.EscapeDataString(SearchQuery)}";
            if (Guid.TryParse(SelectedChapterIdString, out var chapterId))
                url += $"&chapterId={chapterId}";

            Response = await Http.GetFromJsonAsync<ChapterOfficerSearchResponse>(url);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }

    private string RoleLabel(ChapterOfficerType role) => role switch {
        ChapterOfficerType.Captain => I18n[I18nKey.Ui.OfficerRole.Captain],
        ChapterOfficerType.FirstOfficer => I18n[I18nKey.Ui.OfficerRole.FirstOfficer],
        ChapterOfficerType.Quartermaster => I18n[I18nKey.Ui.OfficerRole.Quartermaster],
        ChapterOfficerType.Treasurer => I18n[I18nKey.Ui.OfficerRole.Treasurer],
        ChapterOfficerType.ViceTreasurer => I18n[I18nKey.Ui.OfficerRole.ViceTreasurer],
        ChapterOfficerType.PoliticalDirector => I18n[I18nKey.Ui.OfficerRole.PoliticalDirector],
        ChapterOfficerType.Member => I18n[I18nKey.Ui.OfficerRole.Member],
        _ => role.ToString()
    };
}
