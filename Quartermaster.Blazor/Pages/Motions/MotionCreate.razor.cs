using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Motions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Motions;

public partial class MotionCreate {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private string SelectedChapterId { get; set; } = "";
    private string AuthorName { get; set; } = "";
    private string AuthorEmail { get; set; } = "";
    private string Title { get; set; } = "";
    private string Text { get; set; } = "";
    private bool Submitting;
    private string? SubmittedEmail;

    private bool CanSubmit() {
        if (string.IsNullOrEmpty(SelectedChapterId))
            return false;
        if (string.IsNullOrEmpty(AuthorName))
            return false;
        if (string.IsNullOrEmpty(AuthorEmail))
            return false;
        if (string.IsNullOrEmpty(Title))
            return false;
        if (string.IsNullOrEmpty(Text))
            return false;
        return true;
    }

    private async Task Submit() {
        if (Submitting || !CanSubmit()) {
            return;
        }
        if (!Guid.TryParse(SelectedChapterId, out var chapterId)) {
            return;
        }
        Submitting = true;
        StateHasChanged();
        try {
            var result = await Http.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
                ChapterId = chapterId,
                AuthorName = AuthorName,
                AuthorEmail = AuthorEmail,
                Title = Title,
                Text = Text
            });

            if (result.IsSuccessStatusCode) {
                SubmittedEmail = AuthorEmail;
                StateHasChanged();
            } else {
                Submitting = false;
                await ToastService.ErrorAsync(result);
                StateHasChanged();
            }
        } catch (HttpRequestException ex) {
            Submitting = false;
            ToastService.Error(ex);
            StateHasChanged();
        }
    }
}
