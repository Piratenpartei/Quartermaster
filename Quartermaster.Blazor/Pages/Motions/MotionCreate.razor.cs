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
    public required NavigationManager NavigationManager { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    private string SelectedChapterId { get; set; } = "";
    private string AuthorName { get; set; } = "";
    private string AuthorEMail { get; set; } = "";
    private string Title { get; set; } = "";
    private string Text { get; set; } = "";

    private bool CanSubmit() {
        if (string.IsNullOrEmpty(SelectedChapterId))
            return false;
        if (string.IsNullOrEmpty(AuthorName))
            return false;
        if (string.IsNullOrEmpty(AuthorEMail))
            return false;
        if (string.IsNullOrEmpty(Title))
            return false;
        if (string.IsNullOrEmpty(Text))
            return false;
        return true;
    }

    private async Task Submit() {
        if (!Guid.TryParse(SelectedChapterId, out var chapterId))
            return;

        var result = await Http.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
            ChapterId = chapterId,
            AuthorName = AuthorName,
            AuthorEMail = AuthorEMail,
            Title = Title,
            Text = Text
        });

        if (result.IsSuccessStatusCode) {
            NavigationManager.NavigateTo("/");
            ToastService.Toast("Dein Antrag wurde eingereicht!", "success");
        } else {
            await ToastService.ErrorAsync(result);
        }
    }
}
