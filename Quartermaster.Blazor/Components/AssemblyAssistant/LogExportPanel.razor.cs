using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyAssistant;

public partial class LogExportPanel {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required IJSRuntime Js { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    [Parameter]
    public bool CanControl { get; set; }

    /// <summary>Changes whenever the session list may have changed (a session started or ended), which reloads it.</summary>
    [Parameter]
    public string? RefreshKey { get; set; }

    private List<AssemblySessionSummaryDTO>? Sessions;
    private ConfirmDialog? DeleteDialog;
    private string? loadedFor;

    protected override async Task OnParametersSetAsync() {
        if (loadedFor == RefreshKey && Sessions != null)
            return;

        loadedFor = RefreshKey;
        await Load();
    }

    private async Task Load() {
        try {
            Sessions = await Http.GetFromJsonAsync<List<AssemblySessionSummaryDTO>>($"/api/assembly/{EventId}/sessions") ?? [];
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            Sessions = [];
        }
    }

    private async Task Download(Guid sessionId, string format) {
        try {
            using var response = await Http.GetAsync($"/api/assembly/sessions/{sessionId}/log?format={format}");
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var disposition = response.Content.Headers.ContentDisposition;
            var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? $"protokollhilfe.{(format == "csv" ? "csv" : "md")}";
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            await Js.InvokeVoidAsync("qmDownload.blob", bytes, fileName, contentType);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task Delete(Guid sessionId) {
        if (DeleteDialog == null || !await DeleteDialog.ShowAsync())
            return;

        try {
            var response = await Http.PostAsJsonAsync($"/api/assembly/sessions/{sessionId}/log/delete", new { });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            await Load();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }
}
