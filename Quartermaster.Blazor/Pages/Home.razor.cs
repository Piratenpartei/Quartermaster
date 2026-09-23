using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Changelog;
using Quartermaster.Api.Dashboard;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class Home {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    private DashboardDTO? Dashboard;
    private List<ChangelogEntryDTO>? Changelog;
    private string? openChangelogVersion;
    private bool Loading = true;

    private bool HasAnyWidget => Dashboard != null
        && (Dashboard.PendingApplications != null
            || Dashboard.PendingDueSelections != null
            || Dashboard.OpenMotions != null
            || (Dashboard.UpcomingEvents != null && Dashboard.UpcomingEvents.Count > 0));

    protected override async Task OnInitializedAsync() {
        try {
            Dashboard = await Http.GetFromJsonAsync<DashboardDTO>("/api/dashboard");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        try {
            Changelog = await Http.GetFromJsonAsync<List<ChangelogEntryDTO>>("/api/changelog");
            if (Changelog != null && Changelog.Count > 0)
                openChangelogVersion = Changelog[0].Version;
        } catch (HttpRequestException) {
            // Changelog is informational; a fetch failure shouldn't toast on every page load.
        }
        Loading = false;
    }

    private void ToggleChangelogVersion(string version) {
        openChangelogVersion = openChangelogVersion == version ? null : version;
    }
}
