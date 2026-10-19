using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Quartermaster.Api.Receipts;

namespace Quartermaster.Blazor.Services;

/// <summary>The logged-in user's receipt cache, loaded once per page and shared by every picker on it.</summary>
public class ReceiptService {
    private const long UploadReadLimitBytes = 64L * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly ToastService _toasts;
    private UserReceiptListDTO? _list;

    public ReceiptService(HttpClient http, IJSRuntime js, ToastService toasts) {
        _http = http;
        _js = js;
        _toasts = toasts;
    }

    public event Action? Changed;

    public async Task<UserReceiptListDTO> ListAsync(bool reload = false) {
        if (_list != null && !reload)
            return _list;

        try {
            _list = await _http.GetFromJsonAsync<UserReceiptListDTO>("/api/receipts") ?? new UserReceiptListDTO();
        } catch (HttpRequestException ex) {
            _toasts.Error(ex);
            _list = new UserReceiptListDTO();
        }
        return _list;
    }

    /// <summary>Uploads the file; null when the server refused it (the toast says why).</summary>
    public async Task<UserReceiptDTO?> UploadAsync(IBrowserFile file, string? label = null) {
        try {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream(UploadReadLimitBytes);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "File", file.Name);
            if (!string.IsNullOrWhiteSpace(label))
                content.Add(new StringContent(label), "Label");

            var response = await _http.PostAsync("/api/receipts", content);
            if (!response.IsSuccessStatusCode) {
                await _toasts.ErrorAsync(response);
                return null;
            }

            var created = await response.Content.ReadFromJsonAsync<UserReceiptDTO>();
            await ListAsync(reload: true);
            Changed?.Invoke();
            return created;
        } catch (Exception ex) when (ex is HttpRequestException || ex is IOException) {
            _toasts.Error(ex.Message);
            return null;
        }
    }

    public async Task<bool> RenameAsync(Guid id, string label) {
        var response = await _http.PutAsJsonAsync($"/api/receipts/{id}", new UserReceiptRenameRequest { Id = id, Label = label });
        if (!response.IsSuccessStatusCode) {
            await _toasts.ErrorAsync(response);
            return false;
        }
        await ListAsync(reload: true);
        Changed?.Invoke();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id) {
        var response = await _http.DeleteAsync($"/api/receipts/{id}");
        if (!response.IsSuccessStatusCode) {
            await _toasts.ErrorAsync(response);
            return false;
        }
        await ListAsync(reload: true);
        Changed?.Invoke();
        return true;
    }

    public async Task DownloadAsync(UserReceiptDTO receipt) {
        var response = await _http.GetAsync($"/api/receipts/{receipt.Id}/file");
        if (!response.IsSuccessStatusCode) {
            await _toasts.ErrorAsync(response);
            return;
        }
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await _js.InvokeVoidAsync("qmDownload.blob", bytes, receipt.FileName, receipt.ContentType);
    }
}
