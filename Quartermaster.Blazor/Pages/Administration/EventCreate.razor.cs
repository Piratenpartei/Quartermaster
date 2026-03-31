using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Events;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class EventCreate {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private string ChapterId { get; set; } = "";
    private string InternalName { get; set; } = "";
    private bool Submitting;

    private async Task Submit() {
        if (!Guid.TryParse(ChapterId, out var chapterId) ||
            string.IsNullOrWhiteSpace(InternalName))
            return;

        Submitting = true;
        StateHasChanged();

        var response = await Http.PostAsJsonAsync("/api/events", new EventCreateRequest {
            ChapterId = chapterId,
            InternalName = InternalName,
            PublicName = InternalName,
        });

        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<EventDetailDTO>();
            if (result != null)
                Navigation.NavigateTo($"/Administration/Events/{result.Id}");
        }

        Submitting = false;
        StateHasChanged();
    }
}
