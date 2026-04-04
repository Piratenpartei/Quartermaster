using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Events;

public partial class PublicEventList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private EventSearchResponse? Response;
    private bool Loading = true;

    protected override async Task OnInitializedAsync() {
        try {
            // Anonymous access returns only Public events; the server filters by visibility.
            Response = await Http.GetFromJsonAsync<EventSearchResponse>("/api/events?page=1&pageSize=50");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }
}
