using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.ChapterMails;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Mailings;

public partial class MailingList {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }

    private ChapterMailListResponse? Response;
    private bool Loading = true;
    private bool IncludeArchived;

    private bool ShowNewDialog;
    private string NewMailChapterId = "";
    private string NewMailTitle = "";
    private bool Creating;

    protected override async Task OnInitializedAsync() {
        await LoadList();
    }

    private async Task LoadList() {
        Loading = true;
        StateHasChanged();
        try {
            var url = $"/api/chapter-mail?includeArchived={IncludeArchived}";
            Response = await Http.GetFromJsonAsync<ChapterMailListResponse>(url);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
        StateHasChanged();
    }

    private string RenderTarget(ChapterMailListItemDTO m) {
        var typeLabel = m.TargetType == ChapterMailTargetType.Chapter
            ? I18n[I18nKey.Ui.Mailings.TargetTypeChapter]
            : I18n[I18nKey.Ui.Mailings.TargetTypeAdminDivision];
        return string.IsNullOrEmpty(m.TargetName) ? typeLabel : $"{typeLabel}: {m.TargetName}";
    }

    private string RenderStatus(ChapterMailListItemDTO m) {
        if (m.IsArchived)
            return I18n[I18nKey.Ui.Mailings.StatusArchived];
        if (m.IsSent)
            return I18n[I18nKey.Ui.Mailings.StatusSent];
        return I18n[I18nKey.Ui.Mailings.StatusDraft];
    }

    private void ShowNewMailDialog() {
        NewMailChapterId = "";
        NewMailTitle = "";
        ShowNewDialog = true;
    }

    private void CancelNewMail() {
        ShowNewDialog = false;
    }

    private void OnNewMailChapterChanged(string value) {
        NewMailChapterId = value;
    }

    private async Task CreateNewMail() {
        if (string.IsNullOrWhiteSpace(NewMailChapterId) || string.IsNullOrWhiteSpace(NewMailTitle))
            return;
        if (!Guid.TryParse(NewMailChapterId, out var chapterId))
            return;

        Creating = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync("/api/chapter-mail", new ChapterMailCreateRequest {
                ChapterId = chapterId,
                Title = NewMailTitle.Trim(),
                TargetType = ChapterMailTargetType.Chapter,
                TargetId = chapterId,
                IncludeDescendants = true
            });
            if (resp.IsSuccessStatusCode) {
                var body = await resp.Content.ReadFromJsonAsync<ChapterMailCreateResponse>();
                if (body != null) {
                    ShowNewDialog = false;
                    Navigation.NavigateTo($"/Administration/Mailings/{body.Id}");
                    return;
                }
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Creating = false;
        StateHasChanged();
    }
}

public class ChapterMailCreateResponse {
    public Guid Id { get; set; }
}
