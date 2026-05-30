using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;
using Quartermaster.Api.Submissions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class MotionCreate {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    private string ChapterId { get; set; } = "";
    private string AuthorName { get; set; } = "";
    private string AuthorEmail { get; set; } = "";
    private string Title { get; set; } = "";
    private string Text { get; set; } = "";
    private bool Submitting;

    protected override async Task OnInitializedAsync() {
        await AuthService.WaitForInitializationAsync();
        var user = AuthService.CurrentUser;
        if (user != null) {
            AuthorName = user.DisplayName;
            AuthorEmail = user.Email;
        }
    }

    private async Task Submit() {
        if (!Guid.TryParse(ChapterId, out var chapterId) ||
            string.IsNullOrWhiteSpace(AuthorName) ||
            string.IsNullOrWhiteSpace(Title))
            return;

        Submitting = true;
        StateHasChanged();

        try {
            var response = await Http.PostAsJsonAsync("/api/motions", new MotionCreateRequest {
                ChapterId = chapterId,
                AuthorName = AuthorName,
                AuthorEmail = AuthorEmail,
                Title = Title,
                Text = Text
            });

            if (response.IsSuccessStatusCode) {
                var result = await response.Content.ReadFromJsonAsync<SubmissionAcceptedResponse>();
                if (result?.CreatedEntityId != null) {
                    ToastService.ToastKey(I18nKey.Ui.Toast.MotionCreated);
                    Navigation.NavigateTo($"/Administration/Motions/{result.CreatedEntityId.Value}");
                }
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Submitting = false;
        StateHasChanged();
    }
}
