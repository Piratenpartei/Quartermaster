using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Dashboard;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class Home {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private DashboardDTO? Dashboard;
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
        Loading = false;
    }
}
