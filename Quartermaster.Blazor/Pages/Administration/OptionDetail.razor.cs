using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Options;
using Quartermaster.Rendering;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OptionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private OptionDefinitionDTO? Option;
    private List<ChapterDTO>? Chapters;
    private bool Loading = true;
    private string NewOverrideChapterId { get; set; } = "";
    private string? PreviewHtml;
    private bool ShowPreview;
    private OptionOverrideDTO? EditingOverride;
    private string EditingOverrideValue { get; set; } = "";
    private DirtyForm _globalForm = default!;
    private CancellationTokenSource? _previewDebounce;

    protected override async Task OnInitializedAsync() {
        try {
            Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");

            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
            Option = options?.FirstOrDefault(o => o.Id == Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private async Task OnTemplateValueChanged(string value) {
        if (Option == null)
            return;

        Option.GlobalValue = value;

        if (!ShowPreview)
            return;

        _previewDebounce?.Cancel();
        _previewDebounce = new CancellationTokenSource();
        var token = _previewDebounce.Token;

        try {
            await Task.Delay(500, token);
            await UpdatePreview();
        } catch (TaskCanceledException) { }
    }

    private async Task SaveGlobal() {
        if (Option == null)
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = null,
                Value = Option.GlobalValue
            });
            _globalForm.Reset();
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SaveOverride(Guid chapterId, string value) {
        if (Option == null)
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = chapterId,
                Value = value
            });
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task AddOverride() {
        if (Option == null || !Guid.TryParse(NewOverrideChapterId, out var chapterId))
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = chapterId,
                Value = Option.GlobalValue
            });

            NewOverrideChapterId = "";
            await ReloadOption();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void LoadOverrideForEditing(OptionOverrideDTO ov) {
        EditingOverride = ov;
        EditingOverrideValue = ov.Value;
        StateHasChanged();
    }

    private async Task SaveEditingOverride() {
        if (EditingOverride == null || Option == null)
            return;

        await SaveOverride(EditingOverride.ChapterId, EditingOverrideValue);
        EditingOverride = null;
        await ReloadOption();
    }

    private void CancelEditingOverride() {
        EditingOverride = null;
        StateHasChanged();
    }

    private async Task TogglePreview() {
        if (Option == null)
            return;

        ShowPreview = !ShowPreview;

        if (ShowPreview) {
            await UpdatePreview();
        } else {
            StateHasChanged();
        }
    }

    private async Task UpdatePreview() {
        if (Option == null)
            return;

        var mockData = TemplateMockDataProvider.GetMockData(Option.TemplateModels);
        var (html, error) = await TemplateRenderer.RenderHtmlAsync(Option.GlobalValue, mockData);

        if (error != null)
            PreviewHtml = $"<p class=\"text-danger\">{error}</p>";
        else
            PreviewHtml = html ?? "";

        StateHasChanged();
    }

    private async Task InsertField(string fluidExpression) {
        if (Option == null)
            return;

        Option.GlobalValue += fluidExpression;

        if (ShowPreview) {
            await UpdatePreview();
        } else {
            StateHasChanged();
        }
    }

    private async Task ReloadOption() {
        try {
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
            Option = options?.FirstOrDefault(o => o.Id == Id);
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private static string DataTypeLabel(OptionDataType dt) => dt switch {
        OptionDataType.String => "Text",
        OptionDataType.Number => "Zahl",
        OptionDataType.Template => "Template",
        _ => "?"
    };

    private static string DataTypeBadge(OptionDataType dt) => dt switch {
        OptionDataType.String => "border-info text-info-emphasis",
        OptionDataType.Number => "border-primary text-primary-emphasis",
        OptionDataType.Template => "border-warning text-warning-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
