using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Templates;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;
using Quartermaster.Rendering;

namespace Quartermaster.Blazor.Pages.Administration.Mailings;

public partial class MailingDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private ChapterMailDTO? Mail;
    private bool Loading = true;
    private bool Saving;

    private string EditTitle = "";
    private string EditSubject = "";
    private ChapterMailTargetType EditTargetType;
    private string EditTargetId = "";
    private bool EditIncludeDescendants = true;
    private Guid? EditTemplateId;
    private string EditContent = "";
    private bool UseTemplateMode = true;
    private string? TemplateBody;
    private string TemplatePreviewHtml = "";

    private bool SendDisabled => UseTemplateMode
        ? EditTemplateId == null
        : string.IsNullOrWhiteSpace(EditSubject) || string.IsNullOrWhiteSpace(EditContent);

    private string? SendDisabledReason {
        get {
            if (!SendDisabled)
                return null;
            return UseTemplateMode
                ? I18n[I18nKey.Ui.Mailings.SendDisabledMissingTemplate]
                : I18n[I18nKey.Ui.Mailings.SendDisabledMissingManualContent];
        }
    }

    private List<RecipientPreviewItem>? Recipients;

    private ConfirmDialog DeleteConfirm = default!;
    private ConfirmDialog SendConfirm = default!;

    private bool ReadOnly => Mail != null && (Mail.IsSent || Mail.IsArchived);

    protected override async Task OnParametersSetAsync() {
        await LoadMail();
    }

    private async Task LoadMail() {
        Loading = true;
        Recipients = null;
        StateHasChanged();
        try {
            Mail = await Http.GetFromJsonAsync<ChapterMailDTO>($"/api/chapter-mail/{Id}");
            if (Mail != null) {
                EditTitle = Mail.Title;
                EditSubject = Mail.Subject ?? "";
                EditTargetType = Mail.TargetType;
                EditTargetId = Mail.TargetId.ToString();
                EditIncludeDescendants = Mail.IncludeDescendants;
                EditTemplateId = Mail.TemplateId;
                EditContent = Mail.Content ?? "";
                UseTemplateMode = Mail.TemplateId.HasValue
                    || (string.IsNullOrEmpty(Mail.Subject) && string.IsNullOrEmpty(Mail.Content));
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        if (UseTemplateMode && EditTemplateId.HasValue) {
            await LoadTemplatePreview();
        }
        StateHasChanged();
    }

    private void OnTargetIdChanged(string value) {
        EditTargetId = value;
    }

    private void OnTargetTypeChanged() {
        EditTargetId = "";
    }

    private async Task OnTemplateChanged(Guid? value) {
        EditTemplateId = value;
        await LoadTemplatePreview();
    }

    private async Task LoadTemplatePreview() {
        TemplateBody = null;
        TemplatePreviewHtml = "";
        if (EditTemplateId == null) {
            StateHasChanged();
            return;
        }
        try {
            var detail = await Http.GetFromJsonAsync<TemplateDetailDTO>($"/api/templates/{EditTemplateId}");
            if (detail != null) {
                TemplateBody = detail.Body;
                var mockData = TemplateMockDataProvider.GetMockData("MemberDetailDTO,ChapterDTO");
                var (html, _) = await TemplateRenderer.RenderHtmlAsync(detail.Body, mockData);
                TemplatePreviewHtml = html ?? "";
            }
        } catch (HttpRequestException) {
            TemplatePreviewHtml = "";
        }
        StateHasChanged();
    }

    private void SwitchMode(bool useTemplate) {
        UseTemplateMode = useTemplate;
        if (useTemplate) {
            EditSubject = "";
            EditContent = "";
        } else {
            EditTemplateId = null;
        }
    }

    private void OnContentChanged(string value) {
        EditContent = value;
    }

    private async Task Save() {
        if (Mail == null)
            return;
        if (!Guid.TryParse(EditTargetId, out var targetId)) {
            ToastService.Error("Bitte ein Ziel auswählen.");
            return;
        }

        Saving = true;
        StateHasChanged();
        try {
            var resp = await Http.PutAsJsonAsync($"/api/chapter-mail/{Mail.Id}", new ChapterMailUpdateRequest {
                Title = EditTitle.Trim(),
                Subject = UseTemplateMode ? null : EditSubject.Trim(),
                TargetType = EditTargetType,
                TargetId = targetId,
                IncludeDescendants = EditIncludeDescendants,
                TemplateId = UseTemplateMode ? EditTemplateId : null,
                Content = UseTemplateMode ? null : EditContent
            });
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                await LoadMail();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Saving = false;
        StateHasChanged();
    }

    private async Task LoadRecipients() {
        if (Mail == null)
            return;
        try {
            var resp = await Http.PostAsJsonAsync($"/api/chapter-mail/{Mail.Id}/recipient-preview", new { });
            if (resp.IsSuccessStatusCode) {
                var body = await resp.Content.ReadFromJsonAsync<RecipientPreviewResponse>();
                Recipients = body?.Recipients ?? new List<RecipientPreviewItem>();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        StateHasChanged();
    }

    private async Task ConfirmSend() {
        if (Mail == null)
            return;
        if (Recipients == null) {
            await LoadRecipients();
        }
        var count = Recipients?.Count ?? 0;
        var ok = await SendConfirm.ShowAsync(I18n[$"{I18nKey.Ui.Mailings.SendConfirmBody}?count={count}"]);
        if (!ok)
            return;

        try {
            var resp = await Http.PostAsJsonAsync($"/api/chapter-mail/{Mail.Id}/send", new { });
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
                await LoadMail();
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ConfirmDelete() {
        if (Mail == null)
            return;
        var msgKey = Mail.IsSent
            ? I18nKey.Ui.Mailings.DeleteConfirmSent
            : I18nKey.Ui.Mailings.DeleteConfirmDraft;
        var ok = await DeleteConfirm.ShowAsync(I18n[msgKey]);
        if (!ok)
            return;

        try {
            var resp = await Http.DeleteAsync($"/api/chapter-mail/{Mail.Id}");
            if (resp.IsSuccessStatusCode) {
                Navigation.NavigateTo("/Administration/Mailings");
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
