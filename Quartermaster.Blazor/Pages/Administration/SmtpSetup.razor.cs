using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Email;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class SmtpSetup {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private static readonly string[] SmtpKeys = [
        "email.smtp.host",
        "email.smtp.port",
        "email.smtp.use_ssl",
        "email.smtp.username",
        "email.smtp.password",
        "email.smtp.sender_address",
        "email.smtp.sender_name",
        "email.smtp.batch_size"
    ];

    private string TestRecipient = "";
    private bool Sending;
    private EmailTestResultDTO? TestResult;

    private async Task SendTest() {
        Sending = true;
        TestResult = null;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync("/api/email/test", new EmailTestRequest { Recipient = TestRecipient });
            if (resp.IsSuccessStatusCode) {
                TestResult = await resp.Content.ReadFromJsonAsync<EmailTestResultDTO>();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Sending = false;
            StateHasChanged();
        }
    }
}
