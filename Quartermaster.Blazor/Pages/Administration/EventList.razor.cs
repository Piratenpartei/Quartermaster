using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

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

    private static string StatusToLabel(EventStatus s) => s switch {
        EventStatus.Draft => "Entwurf",
        EventStatus.Active => "Aktiv",
        EventStatus.Completed => "Abgeschlossen",
        EventStatus.Archived => "Archiviert",
        _ => s.ToString()
    };

    private static string VisibilityToLabel(EventVisibility v) => v switch {
        EventVisibility.Public => "Öffentlich",
        EventVisibility.MembersOnly => "Mitglieder",
        EventVisibility.Private => "Intern",
        _ => v.ToString()
    };

    private static string GetStatusBadgeClass(EventStatus s) => s switch {
        EventStatus.Draft => "border-secondary text-secondary-emphasis",
        EventStatus.Active => "border-primary text-primary-emphasis",
        EventStatus.Completed => "border-success text-success-emphasis",
        EventStatus.Archived => "border-secondary text-body-tertiary",
        _ => "border-secondary"
    };

    private static string GetVisibilityBadgeClass(EventVisibility v) => v switch {
        EventVisibility.Public => "border-info text-info-emphasis",
        EventVisibility.MembersOnly => "border-primary text-primary-emphasis",
        EventVisibility.Private => "border-secondary text-secondary-emphasis",
        _ => "border-secondary"
    };

    private async Task Search() {
        Loading = true;
        StateHasChanged();

        try {
            var url = $"/api/events?page={CurrentPage}&pageSize={PageSize}&includeArchived={IncludeArchived}";
            if (Guid.TryParse(SelectedChapterIdString, out var chapterId))
                url += $"&chapterId={chapterId}";

            Response = await Http.GetFromJsonAsync<EventSearchResponse>(url);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
        StateHasChanged();
    }
}
