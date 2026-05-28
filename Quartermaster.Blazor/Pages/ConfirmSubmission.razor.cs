using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Submissions;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages;

public partial class ConfirmSubmission {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public string Token { get; set; } = "";

    private bool Loading = true;
    private SubmissionConfirmStatus Result = SubmissionConfirmStatus.NotFound;

    protected override async Task OnInitializedAsync() {
        try {
            var resp = await Http.PostAsync($"/api/submissions/{Token}/confirm", null);
            if (resp.IsSuccessStatusCode) {
                var dto = await resp.Content.ReadFromJsonAsync<SubmissionConfirmResultDTO>();
                Result = dto?.Status ?? SubmissionConfirmStatus.NotFound;
            } else {
                Result = SubmissionConfirmStatus.NotFound;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Result = SubmissionConfirmStatus.NotFound;
        }
        Loading = false;
    }
}
