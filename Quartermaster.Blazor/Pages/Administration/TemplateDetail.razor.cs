using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Templates;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Components.Inputs;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class TemplateDetail {
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

    private TemplateDetailDTO? Template;
    private bool Loading = true;
    private bool NoSubject;
    private DirtyForm _form = default!;
    private ConfirmDialog ConfirmDialog = default!;
    private MarkdownEditor? _bodyEditor;

    private TemplateOverrideDTO? EditingOverride;
    private string EditingOverrideChapterIdRaw { get; set; } = "";

    private string PaletteModels => Template == null
        ? ""
        : TemplateModelLookup.BuildForTemplate(Template.Identifier, Template.AllowsChapterFields, Template.AllowsMemberFields, Template.AllowsEventFields);

    private async Task InsertField(string fluidExpression) {
        if (_bodyEditor == null)
            return;
        await _bodyEditor.InsertAtCursorAsync(fluidExpression);
        _form?.MarkDirty();
    }

    protected override async Task OnInitializedAsync() {
        await Reload();
    }

    private async Task Reload() {
        Loading = true;
        try {
            Template = await Http.GetFromJsonAsync<TemplateDetailDTO>($"/api/templates/{Id}");
            NoSubject = Template?.Subject == null;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    private void ToggleNoSubject(ChangeEventArgs e) {
        if (Template == null)
            return;

        NoSubject = (bool)(e.Value ?? false);
        Template.Subject = NoSubject ? null : (Template.Subject ?? "");
    }

    private async Task Save() {
        if (Template == null)
            return;

        try {
            var response = await Http.PutAsJsonAsync($"/api/templates/{Template.Id}", new TemplateUpdateRequest {
                Id = Template.Id,
                DisplayName = Template.DisplayName,
                Subject = Template.Subject,
                Body = Template.Body,
                AllowsMemberFields = Template.AllowsMemberFields,
                AllowsEventFields = Template.AllowsEventFields,
                AllowsChapterFields = Template.AllowsChapterFields
            });
            response.EnsureSuccessStatusCode();
            _form.Reset();
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task DeleteTemplate() {
        if (Template == null)
            return;

        if (!await ConfirmDialog.ShowAsync(I18n[I18nKey.Ui.TemplateDetail.DeleteConfirm]))
            return;

        try {
            var response = await Http.DeleteAsync($"/api/templates/{Template.Id}");
            response.EnsureSuccessStatusCode();
            Navigation.NavigateTo("/Administration/Templates");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void StartAddOverride() {
        EditingOverride = new TemplateOverrideDTO {
            Id = Guid.Empty,
            Subject = "",
            Body = ""
        };
        EditingOverrideChapterIdRaw = "";
    }

    private void StartEditOverride(TemplateOverrideDTO ov) {
        EditingOverride = new TemplateOverrideDTO {
            Id = ov.Id,
            ChapterId = ov.ChapterId,
            ChapterName = ov.ChapterName,
            ChapterShortCode = ov.ChapterShortCode,
            Subject = ov.Subject,
            Body = ov.Body
        };
        EditingOverrideChapterIdRaw = ov.ChapterId.ToString();
    }

    private void CancelEditOverride() {
        EditingOverride = null;
    }

    private async Task SaveOverride() {
        if (EditingOverride == null || Template == null)
            return;

        if (!Guid.TryParse(EditingOverrideChapterIdRaw, out var chapterId))
            return;

        try {
            var response = await Http.PostAsJsonAsync($"/api/templates/{Template.Id}/overrides", new TemplateOverrideUpsertRequest {
                TemplateId = Template.Id,
                ChapterId = chapterId,
                Subject = EditingOverride.Subject,
                Body = EditingOverride.Body
            });
            response.EnsureSuccessStatusCode();
            EditingOverride = null;
            await Reload();
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private static string TruncateBody(string body) {
        if (string.IsNullOrEmpty(body))
            return "";
        return body.Length <= 80 ? body : body[..80] + "…";
    }
}
