using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MotionCreate {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private string ChapterId { get; set; } = "";
    private string AuthorName { get; set; } = "";
    private string AuthorEMail { get; set; } = "";
    private string Title { get; set; } = "";
    private string Text { get; set; } = "";
    private bool Submitting;

    private async Task Submit() {
        if (!Guid.TryParse(ChapterId, out var chapterId) ||
            string.IsNullOrWhiteSpace(AuthorName) ||
            string.IsNullOrWhiteSpace(Title))
            return;

        Submitting = true;
        StateHasChanged();

        var response = await Http.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = AuthorName,
            AuthorEMail = AuthorEMail,
            Title = Title,
            Text = Text
        });

        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<MotionDTO>();
            if (result != null)
                Navigation.NavigateTo($"/Administration/Motions/{result.Id}");
        }

        Submitting = false;
        StateHasChanged();
    }
}
