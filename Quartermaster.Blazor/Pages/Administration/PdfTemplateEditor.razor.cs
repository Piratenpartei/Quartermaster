using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.Forms;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.Inputs;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class PdfTemplateEditor : IDisposable {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required IJSRuntime JS { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }

    [Parameter]
    public string Key { get; set; } = "";

    private PdfTemplateDTO? Template;
    private string _body = "";
    private string Body {
        get => _body;
        set {
            if (_body == value) {
                return;
            }
            _body = value;
            ScheduleAutoPreview();
        }
    }

    private bool Loading = true;
    private bool Busy;
    private bool Previewing;
    private bool AutoPreview = true;
    private string? PreviewError;
    private ElementReference PreviewFrame;
    private CodeMirrorEditor? Editor;
    private ConfirmDialog ResetConfirm = default!;
    private Timer? _debounceTimer;
    private bool _firstPreviewDone;

    private enum RightTab { Preview, Fields }
    private RightTab ActiveTab = RightTab.Preview;

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        _firstPreviewDone = false;
        try {
            Template = await Http.GetFromJsonAsync<PdfTemplateDTO>($"/api/pdf-templates/{Key}");
            if (Template != null) {
                _body = Template.Body;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (Template != null && !_firstPreviewDone) {
            _firstPreviewDone = true;
            await RefreshPreview();
        }
    }

    private void ScheduleAutoPreview() {
        if (!AutoPreview) {
            return;
        }
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ => _ = InvokeAsync(RefreshPreview), null, 600, Timeout.Infinite);
    }

    private async Task RefreshPreview() {
        Previewing = true;
        PreviewError = null;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync($"/api/pdf-templates/{Key}/preview", new PdfTemplatePreviewRequest { Body = _body });
            if (resp.IsSuccessStatusCode) {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                await JS.InvokeVoidAsync("qmPdfPreview.show", PreviewFrame, bytes);
            } else {
                PreviewError = I18n[$"{I18nKey.Ui.PdfTemplates.PreviewError}?error={await ExtractErrorAsync(resp)}"];
            }
        } catch (HttpRequestException ex) {
            PreviewError = I18n[$"{I18nKey.Ui.PdfTemplates.PreviewError}?error={ex.Message}"];
        }
        Previewing = false;
        StateHasChanged();
    }

    private async Task InsertField(string fluidExpression) {
        if (Editor != null) {
            await Editor.InsertAtCursorAsync(fluidExpression);
        }
    }

    private async Task Save() {
        Busy = true;
        StateHasChanged();
        try {
            var resp = await Http.PutAsJsonAsync($"/api/pdf-templates/{Key}", new PdfTemplateSaveRequest { Body = _body });
            if (resp.IsSuccessStatusCode) {
                ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Busy = false;
        StateHasChanged();
    }

    private async Task ResetToDefault() {
        var ok = await ResetConfirm.ShowAsync(I18n[I18nKey.Ui.PdfTemplates.ResetConfirm]);
        if (!ok) {
            return;
        }
        Busy = true;
        StateHasChanged();
        try {
            var resp = await Http.PostAsJsonAsync($"/api/pdf-templates/{Key}/reset", new { });
            if (resp.IsSuccessStatusCode) {
                var restored = await resp.Content.ReadFromJsonAsync<PdfTemplateDTO>();
                if (restored != null) {
                    _body = restored.Body;
                    Template = restored;
                    await RefreshPreview();
                }
            } else {
                await ToastService.ErrorAsync(resp);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Busy = false;
        StateHasChanged();
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage resp) {
        try {
            var body = await resp.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(body) ? resp.StatusCode.ToString() : body;
        } catch (Exception) {
            return resp.StatusCode.ToString();
        }
    }

    public void Dispose() {
        _debounceTimer?.Dispose();
    }
}
