using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class ImapSetup {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private OptionGroupEditor? ImapEditor;
    private OptionGroupEditor? MdnEditor;
    private bool Copying;
    private string SmtpSenderFallback = "";

    private static readonly (string Source, string Target)[] SmtpToImapMap = [
        ("email.smtp.host", "email.imap.host"),
        ("email.smtp.use_ssl", "email.imap.use_ssl"),
        ("email.smtp.username", "email.imap.username"),
        ("email.smtp.password", "email.imap.password")
    ];

    protected override async Task OnInitializedAsync() {
        await RefreshSmtpFallback();
    }

    private async Task RefreshSmtpFallback() {
        try {
            var all = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? new();
            var sender = all.FirstOrDefault(o => o.Identifier == "email.smtp.sender_address");
            SmtpSenderFallback = sender?.GlobalValue ?? "";
        } catch (HttpRequestException) {
            SmtpSenderFallback = "";
        }
    }

    private async Task CopyFromSmtp() {
        Copying = true;
        StateHasChanged();
        try {
            var all = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? new();
            var byId = all.ToDictionary(o => o.Identifier);

            foreach (var (source, target) in SmtpToImapMap) {
                if (!byId.TryGetValue(source, out var sourceDef))
                    continue;
                if (string.IsNullOrEmpty(sourceDef.GlobalValue))
                    continue;
                var resp = await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                    Identifier = target,
                    ChapterId = null,
                    Value = sourceDef.GlobalValue
                });
                if (!resp.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(resp);
                    return;
                }
            }
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            if (ImapEditor != null)
                await ImapEditor.ReloadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Copying = false;
            StateHasChanged();
        }
    }
}
