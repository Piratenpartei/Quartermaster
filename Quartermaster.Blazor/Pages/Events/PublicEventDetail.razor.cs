using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;
using Quartermaster.Api.Rendering;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Events;

public partial class PublicEventDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private EventDetailDTO? Event;
    private string? DescriptionHtml;
    private bool Loading = true;
    private bool NotFound;

    protected override async Task OnInitializedAsync() {
        try {
            var response = await Http.GetAsync($"/api/events/{Id}");
            if (response.IsSuccessStatusCode) {
                Event = await response.Content.ReadFromJsonAsync<EventDetailDTO>();
                if (Event != null && !string.IsNullOrEmpty(Event.Description)) {
                    DescriptionHtml = ReplaceVariables(
                        MarkdownService.ToHtml(Event.Description, SanitizationProfile.Standard),
                        Event.EventDate);
                }
            } else {
                // 401/403/404 all translate to "not publicly available" for the visitor
                NotFound = true;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            NotFound = true;
        }
        Loading = false;
    }

    private static string ReplaceVariables(string html, DateTime? eventDate) {
        var dateStr = eventDate?.ToString("dd.MM.yyyy") ?? "";
        return html
            .Replace("{{date}}", dateStr)
            .Replace("{{datum}}", dateStr);
    }
}
