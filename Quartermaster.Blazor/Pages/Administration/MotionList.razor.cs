using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MotionList {
    [Inject]
    public required HttpClient Http { get; set; }

    private List<ChapterDTO>? Chapters;
    private MotionListResponse? Response;
    private bool Loading;
    private int CurrentPage = 1;
    private const int PageSize = 25;
    private Guid? SelectedChapterId;
    private int? SelectedStatus = 0;

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

        var url = $"/api/motions?page={CurrentPage}&pageSize={PageSize}&includeNonPublic=true";
        if (SelectedChapterId.HasValue)
            url += $"&chapterId={SelectedChapterId.Value}";
        if (SelectedStatus.HasValue)
            url += $"&approvalStatus={SelectedStatus.Value}";

        Response = await Http.GetFromJsonAsync<MotionListResponse>(url);

        Loading = false;
        StateHasChanged();
    }

    private static string ApprovalLabel(int status) => status switch {
        0 => "Ausstehend",
        1 => "Genehmigt",
        2 => "Abgelehnt",
        3 => "Formal abgelehnt",
        4 => "Ohne Beschluss",
        _ => "Unbekannt"
    };

    private static string ApprovalBadgeClass(int status) => status switch {
        0 => "border-warning text-warning-emphasis",
        1 => "border-success text-success-emphasis",
        2 => "border-danger text-danger-emphasis",
        3 => "border-secondary text-secondary-emphasis",
        4 => "border-secondary text-secondary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
