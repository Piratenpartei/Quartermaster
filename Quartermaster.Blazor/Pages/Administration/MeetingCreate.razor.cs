using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Meetings;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MeetingCreate {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    private string ChapterId { get; set; } = "";
    private string Title { get; set; } = "";
    private DateTime? MeetingDate { get; set; }
    private string? Location { get; set; }
    private string? Description { get; set; }
    private MeetingVisibility Visibility { get; set; } = MeetingVisibility.Private;
    private bool Submitting;

    private async Task Submit() {
        if (!Guid.TryParse(ChapterId, out var chapterId) ||
            string.IsNullOrWhiteSpace(Title)) {
            ToastService.Error("Gliederung und Titel sind erforderlich.");
            return;
        }

        Submitting = true;
        StateHasChanged();

        try {
            var response = await Http.PostAsJsonAsync("/api/meetings", new MeetingCreateRequest {
                ChapterId = chapterId,
                Title = Title,
                Visibility = Visibility,
                MeetingDate = MeetingDate,
                Location = Location,
                Description = Description
            });

            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<MeetingDTO>();
                if (result != null) {
                    ToastService.Toast("Sitzung erstellt.", "success");
                    Navigation.NavigateTo($"/Administration/Meetings/{result.Id}");
                }
            } else {
                var body = await response.Content.ReadAsStringAsync();
                ToastService.Error(details: $"HTTP {(int)response.StatusCode}: {body}");
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Submitting = false;
        StateHasChanged();
    }
}
